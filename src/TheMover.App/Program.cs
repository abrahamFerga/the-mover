// TheMover.App — ARCH.md: Components / Program (Generic Host entry point, ADR-0005)
using System.IO;
using System.Net.Http;
using System.Threading.Channels;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TheMover.App;
using TheMover.App.Calendar;
using TheMover.App.Config;
using TheMover.App.Idle;
using TheMover.App.Logging;
using TheMover.App.Overlay;
using TheMover.App.Scheduler;
using TheMover.App.Shell;
using TheMover.Calendar;
using TheMover.Content;
using TheMover.Scheduler;

var builder = Host.CreateApplicationBuilder(args);

// ServiceDefaults: OTel + health checks + resilience (ADR-0005)
builder.AddServiceDefaults();

// Local user config overrides — stored in %LOCALAPPDATA%\TheMover\appsettings.local.json
var localConfigPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "TheMover", "appsettings.local.json");

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile(localConfigPath, optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services
    .AddOptions<AppSettings>()
    .Bind(builder.Configuration)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// In-process event channels (bounded capacity 1 — DropOldest for BreakDueEvent)
builder.Services.AddSingleton(
    Channel.CreateBounded<BreakDueEvent>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleWriter = true
    }));

builder.Services.AddSingleton(
    Channel.CreateUnbounded<BreakCommand>(new UnboundedChannelOptions
    {
        SingleReader = true
    }));

// Shared in-memory state
builder.Services.AddSingleton<BreakTimerState>();

// Calendar integration — GraphCalendarClient registered as ICalendarClient; requires Azure AD
// App Registration with Calendars.Read scope. TenantId + ClientId configured in appsettings.
builder.Services.AddHttpClient("calendar");
builder.Services.AddSingleton<ICalendarClient>(sp =>
{
    var options = sp.GetRequiredService<IOptionsMonitor<AppSettings>>();
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("calendar");
    var cal = options.CurrentValue.Calendar;
    // Pass a live-credential factory so ConnectAsync rebuilds the PCA from the
    // current settings, letting users connect immediately after saving credentials
    // in the Settings window without restarting the app.
    return new GraphCalendarClient(
        clientId: cal.ClientId ?? string.Empty,
        tenantId: cal.TenantId ?? "common",
        httpClient: http,
        getCredentials: () =>
        {
            var c = options.CurrentValue.Calendar;
            return (c.ClientId ?? string.Empty, c.TenantId ?? "common");
        });
});

// App services
builder.Services.AddSingleton<ConfigManager>();
builder.Services.AddSingleton<EventLogger>();
builder.Services.AddSingleton<StartupRegistrar>();
builder.Services.AddSingleton<ExercisePicker>();
builder.Services.AddSingleton<TrayIconService>();

// WPF Application (created once on the STA thread)
builder.Services.AddSingleton<Application, App>();

// Hosted services — WPF first so the STA thread is up, then scheduler, then overlay + tray
builder.Services.AddHostedService<WpfHostedService>();
builder.Services.AddHostedService<BreakSchedulerService>();
builder.Services.AddHostedService<BreakCommandHandlerService>();
builder.Services.AddHostedService<CalendarSyncService>();
builder.Services.AddHostedService<IdleMonitorService>();
builder.Services.AddHostedService<OverlayService>();
builder.Services.AddHostedService<HeartbeatService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TrayIconService>());

var host = builder.Build();
await host.RunAsync();
