// TheMover.App — ARCH.md: Components / Program (Generic Host entry point, ADR-0005)
using System.IO;
using System.Threading.Channels;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TheMover.App;
using TheMover.App.Config;
using TheMover.App.Logging;
using TheMover.App.Scheduler;
using TheMover.App.Shell;
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

// App services
builder.Services.AddSingleton<ConfigManager>();
builder.Services.AddSingleton<EventLogger>();
builder.Services.AddSingleton<StartupRegistrar>();
builder.Services.AddSingleton<TrayIconService>();

// WPF Application (created once on the STA thread)
builder.Services.AddSingleton<Application, App>();

// Hosted services — WPF first so the STA thread is up, then scheduler, then tray
builder.Services.AddHostedService<WpfHostedService>();
builder.Services.AddHostedService<BreakSchedulerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TrayIconService>());

var host = builder.Build();
await host.RunAsync();
