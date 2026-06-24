// TheMover.App — ARCH.md: SPA architecture / WPF hosting pattern (ADR-0005)
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TheMover.App.Shell;

namespace TheMover.App;

public sealed class WpfHostedService(
    IServiceProvider services,
    ILogger<WpfHostedService> logger) : IHostedService
{
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

            // Show the tray icon once the WPF dispatcher is running.
            app.Startup += (_, _) =>
            {
                var tray = services.GetRequiredService<TrayIconService>();
                tray.ShowTrayIcon();
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
