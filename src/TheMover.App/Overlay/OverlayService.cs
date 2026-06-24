// TheMover.App — reads Channel<BreakDueEvent>, shows OverlayWindow with exercise, signals tray
using System.Threading.Channels;
using System.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheMover.App.Config;
using TheMover.App.Logging;
using TheMover.App.Shell;
using TheMover.Content;
using TheMover.Overlay;
using TheMover.Scheduler;

namespace TheMover.App.Overlay;

public sealed class OverlayService : BackgroundService
{
    private readonly Channel<BreakDueEvent> _breakDueChannel;
    private readonly Channel<BreakCommand> _breakCommandChannel;
    private readonly BreakTimerState _state;
    private readonly IOptionsMonitor<AppSettings> _options;
    private readonly EventLogger _eventLogger;
    private readonly TrayIconService _tray;
    private readonly ExercisePicker _picker;
    private readonly ILogger<OverlayService> _logger;

    public OverlayService(
        Channel<BreakDueEvent> breakDueChannel,
        Channel<BreakCommand> breakCommandChannel,
        BreakTimerState state,
        IOptionsMonitor<AppSettings> options,
        EventLogger eventLogger,
        TrayIconService tray,
        ExercisePicker picker,
        ILogger<OverlayService> logger)
    {
        _breakDueChannel = breakDueChannel;
        _breakCommandChannel = breakCommandChannel;
        _state = state;
        _options = options;
        _eventLogger = eventLogger;
        _tray = tray;
        _picker = picker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in _breakDueChannel.Reader.ReadAllAsync(stoppingToken))
        {
            _eventLogger.Log(AppEventType.BreakFired, new Dictionary<string, object?> { ["tier"] = evt.Tier.ToString() });
            _logger.LogInformation("Break due: {Tier}", evt.Tier);
            _tray.ShowBreakActions();

            await ShowOverlayAsync(evt, stoppingToken);
        }
    }

    private Task ShowOverlayAsync(BreakDueEvent evt, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource();

        Application.Current.Dispatcher.Invoke(() =>
        {
            var settings = _options.CurrentValue;
            var duration = evt.Tier == BreakTier.Long
                ? settings.LongBreak.DurationSeconds
                : settings.MicroBreak.DurationSeconds;
            var snoozeMinutes = settings.Snooze.IncrementMinutes;
            var tierLabel = evt.Tier == BreakTier.Long ? "Long break" : "Micro-break";
            var exercise = _picker.Pick();

            var window = new OverlayWindow(
                tierLabel: tierLabel,
                durationSeconds: duration,
                exercise: exercise,
                onComplete: () =>
                {
                    _eventLogger.Log(AppEventType.BreakCompleted, new Dictionary<string, object?> { ["tier"] = evt.Tier.ToString() });
                    _breakCommandChannel.Writer.TryWrite(new SkipBreakCommand());
                    _tray.HideBreakActions();
                },
                onSnooze: () =>
                {
                    _breakCommandChannel.Writer.TryWrite(new SnoozeBreakCommand(snoozeMinutes));
                    _tray.HideBreakActions();
                    _eventLogger.Log(AppEventType.Snoozed, new Dictionary<string, object?> { ["source"] = "overlay" });
                },
                onSkip: () =>
                {
                    _breakCommandChannel.Writer.TryWrite(new SkipBreakCommand());
                    _tray.HideBreakActions();
                });

            window.Closed += (_, _) =>
            {
                _tray.HideBreakActions();
                tcs.TrySetResult();
            };

            using var reg = ct.Register(() => Application.Current.Dispatcher.Invoke(() =>
            {
                if (window.IsLoaded) window.Close();
            }));

            _eventLogger.Log(AppEventType.OverlayShown, new Dictionary<string, object?>
            {
                ["tier"] = evt.Tier.ToString(),
                ["exerciseId"] = exercise.Id
            });
            window.Show();
        });

        return tcs.Task;
    }
}
