// TheMover.App — ARCH.md: Components / TrayIconService
using System.Drawing;
using System.IO;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TheMover.App.Config;
using TheMover.App.Logging;
using TheMover.App.Settings;
using TheMover.Scheduler;

namespace TheMover.App.Shell;

public sealed class TrayIconService : IHostedService, IDisposable
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<TrayIconService> _logger;
    private readonly EventLogger _eventLogger;
    private readonly BreakTimerState _timerState;
    private readonly Channel<BreakDueEvent> _breakDueChannel;
    private readonly Channel<BreakCommand> _breakCommandChannel;
    private readonly ConfigManager _configManager;
    private TaskbarIcon? _trayIcon;
    private CancellationTokenSource? _cts;

    public TrayIconService(
        IHostApplicationLifetime lifetime,
        ILogger<TrayIconService> logger,
        EventLogger eventLogger,
        BreakTimerState timerState,
        Channel<BreakDueEvent> breakDueChannel,
        Channel<BreakCommand> breakCommandChannel,
        ConfigManager configManager)
    {
        _lifetime = lifetime;
        _logger = logger;
        _eventLogger = eventLogger;
        _timerState = timerState;
        _breakDueChannel = breakDueChannel;
        _breakCommandChannel = breakCommandChannel;
        _configManager = configManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => ListenForBreakEventsAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public void ShowTrayIcon()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _trayIcon = new TaskbarIcon
            {
                Icon = CreateDefaultIcon(),
                ToolTipText = "The Mover — Break reminder active",
                ContextMenu = BuildContextMenu()
            };
            _trayIcon.TrayMouseDoubleClick += (_, _) => OpenSettings();
        });
        _logger.LogInformation("Tray icon initialized");
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var openSettings = new MenuItem { Header = "Open Settings" };
        openSettings.Click += (_, _) => OpenSettings();
        menu.Items.Add(openSettings);

        menu.Items.Add(new Separator());

        var quit = new MenuItem { Header = "Quit" };
        quit.Click += (_, _) => _lifetime.StopApplication();
        menu.Items.Add(quit);

        return menu;
    }

    private void OpenSettings()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new SettingsWindow(_configManager);
            win.Show();
        });
    }

    private async Task ListenForBreakEventsAsync(CancellationToken ct)
    {
        await foreach (var evt in _breakDueChannel.Reader.ReadAllAsync(ct))
        {
            _eventLogger.Log(AppEventType.BreakFired, new Dictionary<string, object?> { ["tier"] = evt.Tier.ToString() });
            _logger.LogInformation("Break due: {Tier} at {FiredAt}", evt.Tier, evt.FiredAt);
        }
    }

    private static Icon CreateDefaultIcon()
    {
        // Placeholder icon — replaced with a real asset in a future epic.
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.DodgerBlue);
        g.FillEllipse(Brushes.White, 3, 3, 10, 10);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        Application.Current?.Dispatcher.Invoke(() => _trayIcon?.Dispose());
        _logger.LogInformation("Tray icon disposed");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _trayIcon?.Dispose();
    }
}
