// TheMover.App — ARCH.md: Components / TrayIconService
using System.Drawing;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheMover.App.Config;
using TheMover.App.Settings;
using TheMover.Calendar;
using TheMover.Scheduler;

namespace TheMover.App.Shell;

public sealed class TrayIconService : IHostedService, IDisposable
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<TrayIconService> _logger;
    private readonly Channel<BreakCommand> _breakCommandChannel;
    private readonly ConfigManager _configManager;
    private readonly IOptionsMonitor<AppSettings> _options;
    private readonly ICalendarClient _calendarClient;
    private readonly StartupRegistrar _startupRegistrar;
    private readonly BreakTimerState _state;

    private TaskbarIcon? _trayIcon;
    private MenuItem? _snoozeItem;
    private MenuItem? _skipItem;
    private DispatcherTimer? _countdownTimer;

    public TrayIconService(
        IHostApplicationLifetime lifetime,
        ILogger<TrayIconService> logger,
        Channel<BreakCommand> breakCommandChannel,
        ConfigManager configManager,
        IOptionsMonitor<AppSettings> options,
        ICalendarClient calendarClient,
        StartupRegistrar startupRegistrar,
        BreakTimerState state)
    {
        _lifetime = lifetime;
        _logger = logger;
        _breakCommandChannel = breakCommandChannel;
        _configManager = configManager;
        _options = options;
        _calendarClient = calendarClient;
        _startupRegistrar = startupRegistrar;
        _state = state;
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

            // Refresh the "Next break in X min" countdown every 30 s on the UI thread.
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _countdownTimer.Tick += (_, _) => RefreshTooltip();
            _countdownTimer.Start();
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
        // Use FiringTier so the Dismissed log records the actual break tier shown,
        // not null ("Unknown") — state.Tier is already the NEXT break by this point.
        _breakCommandChannel.Writer.TryWrite(new SkipBreakCommand(_state.FiringTier));
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

    private bool _isIdle;
    private bool _inMeeting;

    public void UpdateTooltip(bool paused)
    {
        _isIdle = paused;
        RefreshTooltip();
    }

    public void UpdateMeetingTooltip(bool inMeeting)
    {
        _inMeeting = inMeeting;
        RefreshTooltip();
    }

    private void RefreshTooltip()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_trayIcon is null) return;
            _trayIcon.ToolTipText = (_inMeeting, _isIdle) switch
            {
                (true, _) => "The Mover — Paused (in meeting)",
                (_, true) => "The Mover — Paused (idle)",
                _ => BuildActiveTooltip()
            };
        });
    }

    private string BuildActiveTooltip() =>
        BuildActiveTooltipText(_state.NextBreakAt, DateTimeOffset.UtcNow);

    // Extracted for unit testing — all parameters injected so the method is pure.
    internal static string BuildActiveTooltipText(DateTimeOffset nextBreakAt, DateTimeOffset now)
    {
        // Guard against the DateTimeOffset.MaxValue default before the scheduler
        // has called SyncNextBreak — casting MaxValue.TotalMinutes to int overflows.
        if (nextBreakAt == DateTimeOffset.MaxValue)
            return "The Mover — Break reminder active";
        var remaining = nextBreakAt - now;
        if (remaining <= TimeSpan.Zero)
            return "The Mover — Break due";
        var mins = (int)Math.Ceiling(remaining.TotalMinutes);
        return $"The Mover — Next break in {mins} min";
    }

    private void OpenSettings()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new SettingsWindow(_configManager, _calendarClient, _startupRegistrar);
            win.Show();
        });
    }

    private static Icon CreateDefaultIcon()
    {
        // Placeholder icon — replaced with a real asset in a future epic.
        // Dispose bmp after GetHicon() copies its pixels into an HICON so the
        // GDI+ Bitmap object doesn't leak over the lifetime of the app.
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.DodgerBlue);
        g.FillEllipse(Brushes.White, 3, 3, 10, 10);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            _countdownTimer?.Stop();
            _trayIcon?.Dispose();
        });
        _logger.LogInformation("Tray icon disposed");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _countdownTimer?.Stop();
        _trayIcon?.Dispose();
    }
}
