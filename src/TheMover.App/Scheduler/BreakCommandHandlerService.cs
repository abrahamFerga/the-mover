// TheMover.App — reads Channel<BreakCommand> and applies snooze / skip to shared state
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheMover.App.Config;
using TheMover.App.Logging;
using TheMover.Scheduler;

namespace TheMover.App.Scheduler;

public sealed class BreakCommandHandlerService : BackgroundService
{
    private readonly Channel<BreakCommand> _commands;
    private readonly BreakTimerState _state;
    private readonly EventLogger _eventLogger;
    private readonly IOptionsMonitor<AppSettings> _options;
    private readonly ILogger<BreakCommandHandlerService> _logger;

    public BreakCommandHandlerService(
        Channel<BreakCommand> commands,
        BreakTimerState state,
        EventLogger eventLogger,
        IOptionsMonitor<AppSettings> options,
        ILogger<BreakCommandHandlerService> logger)
    {
        _commands = commands;
        _state = state;
        _eventLogger = eventLogger;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var command in _commands.Reader.ReadAllAsync(stoppingToken))
        {
            switch (command)
            {
                case SnoozeBreakCommand snooze:
                    HandleSnooze(snooze.Minutes, snooze.Source);
                    break;

                case SkipBreakCommand skip:
                    HandleSkip(skip.Tier, skip.IsCompletion);
                    break;
            }
        }
    }

    private void HandleSnooze(int minutes, string? source)
    {
        if (minutes <= 0 || minutes > 1440)
        {
            _logger.LogWarning("Ignoring snooze with out-of-range minutes: {Minutes}", minutes);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var settings = _options.CurrentValue;
        // Shift the micro timer so the break re-fires exactly when the snooze expires,
        // not a full interval later.  Without this, a 5-min snooze on a 20-min micro
        // cycle would delay the next reminder by 25 minutes instead of 5.
        _state.LastMicroBreakAt = now
            - TimeSpan.FromMinutes(settings.MicroBreak.IntervalMinutes)
            + TimeSpan.FromMinutes(minutes);

        // Only shift the long-break timer if it was already due at snooze time.
        // Shifting it unconditionally would make the long break fire at snooze expiry
        // even when it last fired recently — e.g. a user snoozing their first micro break
        // (T=20 into a 60-min long cycle) would get a spurious long break at T=25.
        var longInterval = TimeSpan.FromMinutes(settings.LongBreak.IntervalMinutes);
        if (now - _state.LastLongBreakAt >= longInterval)
        {
            _state.LastLongBreakAt = now - longInterval + TimeSpan.FromMinutes(minutes);
        }
        _state.SnoozedUntil = now.AddMinutes(minutes);
        // Update the tray countdown so it shows the snooze expiry time rather than
        // the stale pre-snooze NextBreakAt (SyncNextBreak won't run while paused).
        _state.NextBreakAt = _state.SnoozedUntil.Value;
        _eventLogger.Log(AppEventType.Snoozed, new Dictionary<string, object?>
        {
            ["minutes"] = minutes,
            ["source"] = source ?? "tray"
        });
        _logger.LogInformation("Break snoozed for {Minutes} min (source: {Source})", minutes, source ?? "tray");
    }

    private void HandleSkip(BreakTier? tier, bool isCompletion)
    {
        var now = DateTimeOffset.UtcNow;
        _state.SnoozedUntil = null;
        _state.LastMicroBreakAt = now;
        _state.LastLongBreakAt = now;
        // Mirror the snooze fix: update NextBreakAt immediately so the tray countdown
        // reflects the next break time rather than the stale fire timestamp.
        // BreakSchedulerService calls SyncNextBreak within ≤1 s, but this closes the gap.
        _state.NextBreakAt = now + TimeSpan.FromMinutes(_options.CurrentValue.MicroBreak.IntervalMinutes);
        if (!isCompletion)
        {
            // Use the tier from the command (state.Tier is already the NEXT break by now).
            var tierName = tier?.ToString() ?? "Unknown";
            _eventLogger.Log(AppEventType.Dismissed, new Dictionary<string, object?> { ["tier"] = tierName });
            _logger.LogInformation("Break skipped (tier: {Tier})", tierName);
        }
    }
}
