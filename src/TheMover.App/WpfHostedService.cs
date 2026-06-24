// TheMover.App — ARCH.md: SPA architecture / WPF hosting pattern (ADR-0005)
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheMover.App.Config;
using TheMover.App.Logging;
using TheMover.App.Shell;

namespace TheMover.App;

public sealed class WpfHostedService(
    IServiceProvider services,
    ILogger<WpfHostedService> logger) : IHostedService
{
    private static readonly string LocalDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TheMover");

    private Thread? _uiThread;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _uiThread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            var app = services.GetRequiredService<Application>();
            app.DispatcherUnhandledException += (_, e) =>
            {
                logger.LogError(e.Exception, "Unhandled WPF dispatcher exception");
                e.Handled = true;
            };

            // Show the tray icon and sync the startup registry entry on first launch.
            app.Startup += (_, _) =>
            {
                var tray = services.GetRequiredService<TrayIconService>();
                tray.ShowTrayIcon();

                // Keep HKCU Run in sync with the config so the registry reflects
                // any edits made while the app was not running (e.g. config file edited).
                var settings = services.GetRequiredService<IOptions<AppSettings>>().Value;
                var registrar = services.GetRequiredService<StartupRegistrar>();
                var exePath = Environment.ProcessPath ?? string.Empty;
                registrar.SetStartupEnabled(settings.AutoStartWithWindows, exePath);

                // SPEC: Setup completion rate — log once on the very first launch.
                var stampPath = Path.Combine(LocalDataDir, "firstrun.stamp");
                if (!File.Exists(stampPath))
                {
                    try
                    {
                        Directory.CreateDirectory(LocalDataDir);
                        var eventLogger = services.GetRequiredService<EventLogger>();
                        eventLogger.Log(AppEventType.FirstRunCompleted);
                        File.WriteAllText(stampPath, DateTimeOffset.UtcNow.ToString("O"));
                        logger.LogInformation("First-run event logged");
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not write first-run stamp");
                    }
                }
            };

            app.Run();
        });
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.IsBackground = true;
        _uiThread.Name = "WpfUiThread";
        _uiThread.Start();

        logger.LogInformation("WPF UI thread started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
        logger.LogInformation("WPF application shutdown requested");
        return Task.CompletedTask;
    }
}
