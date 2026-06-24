// TheMover.App — ARCH.md: Components / TrayIconService
using System.Drawing;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheMover.App.Config;
using TheMover.App.Settings;
using TheMover.Scheduler;

namespace TheMover.App.Shell;

public sealed class TrayIconService : IHostedService, IDisposable
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<TrayIconService> _logger;
    private readonly Channel<BreakCommand> _breakCommandChannel;
    private readonly ConfigManager _configManager;
    private readonly IOptionsMonitor<AppSettings> _options;

    private TaskbarIcon? _trayIcon;
    private MenuItem? _snoozeItem;
    private MenuItem? _skipItem;

    public TrayIconService(
        IHostApplicationLifetime lifetime,
        ILogger<TrayIconService> logger,
        Channel<BreakCommand> breakCommandChannel,
        ConfigManager configManager,
        IOptionsMonitor<AppSettings> options)
    {
        _lifetime = lifetime;
        _logger = logger;
        _breakCommandChannel = breakCommandChannel;
        _configManager = configManager;
        _options = options;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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

        _snoozeItem = new MenuItem { Header = "Snooze 5 min", Visibility = Visibility.Collapsed };
        _snoozeItem.Click += OnSnoozeClick;
        menu.Items.Add(_snoozeItem);

        _skipItem = new MenuItem { Header = "Skip break", Visibility = Visibility.Collapsed };
        _skipItem.Click += OnSkipClick;
        menu.Items.Add(_skipItem);

        var breakSeparator = new Separator { Visibility = Visibility.Collapsed };
        menu.Items.Add(breakSeparator);

        var openSettings = new MenuItem { Header = "Open Settings" };
        openSettings.Click += (_, _) => OpenSettings();
        menu.Items.Add(openSettings);

        menu.Items.Add(new Separator());

        var quit = new MenuItem { Header = "Quit" };
        quit.Click += (_, _) => _lifetime.StopApplication();
        menu.Items.Add(quit);

        _snoozeItem.Tag = breakSeparator;

        return menu;
    }

    private void OnSnoozeClick(object sender, RoutedEventArgs e)
    {
        var minutes = _options.CurrentValue.Snooze.IncrementMinutes;
        _breakCommandChannel.Writer.TryWrite(new SnoozeBreakCommand(minutes));
        HideBreakActions();
    }

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        _breakCommandChannel.Writer.TryWrite(new SkipBreakCommand());
        HideBreakActions();
    }

    public void ShowBreakActions()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_snoozeItem is null || _skipItem is null) return;
            _snoozeItem.Visibility = Visibility.Visible;
            _skipItem.Visibility = Visibility.Visible;
            if (_snoozeItem.Tag is Separator sep) sep.Visibility = Visibility.Visible;
        });
    }

    public void HideBreakActions()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_snoozeItem is null || _skipItem is null) return;
            _snoozeItem.Visibility = Visibility.Collapsed;
            _skipItem.Visibility = Visibility.Collapsed;
            if (_snoozeItem.Tag is Separator sep) sep.Visibility = Visibility.Collapsed;
        });
    }

    private void OpenSettings()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new SettingsWindow(_configManager);
            win.Show();
        });
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
        Application.Current?.Dispatcher.Invoke(() => _trayIcon?.Dispose());
        _logger.LogInformation("Tray icon disposed");
        return Task.CompletedTask;
    }

    public void Dispose() => _trayIcon?.Dispose();
}
