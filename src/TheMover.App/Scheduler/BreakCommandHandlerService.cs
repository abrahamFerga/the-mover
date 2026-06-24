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
                    HandleSnooze(snooze.Minutes);
                    break;

                case SkipBreakCommand:
                    HandleSkip();
                    break;
            }
        }
    }

    private void HandleSnooze(int minutes)
    {
        var now = DateTimeOffset.UtcNow;
        var settings = _options.CurrentValue;
        // Shift break timestamps back so the reminder re-fires when the snooze expires,
        // not a full interval later.  Without this, a 5-min snooze on a 20-min micro
        // cycle would delay the next reminder by 25 minutes instead of 5.
        _state.LastMicroBreakAt = now
            - TimeSpan.FromMinutes(settings.MicroBreak.IntervalMinutes)
            + TimeSpan.FromMinutes(minutes);
        _state.LastLongBreakAt = now
            - TimeSpan.FromMinutes(settings.LongBreak.IntervalMinutes)
            + TimeSpan.FromMinutes(minutes);
        _state.SnoozedUntil = now.AddMinutes(minutes);
        _eventLogger.Log(AppEventType.Snoozed, new Dictionary<string, object?> { ["minutes"] = minutes });
        _logger.LogInformation("Break snoozed for {Minutes} min", minutes);
    }

    private void HandleSkip()
    {
        var now = DateTimeOffset.UtcNow;
        _state.SnoozedUntil = null;
        _state.LastMicroBreakAt = now;
        _state.LastLongBreakAt = now;
        _eventLogger.Log(AppEventType.Dismissed, new Dictionary<string, object?> { ["tier"] = _state.Tier.ToString() });
        _logger.LogInformation("Break skipped (tier: {Tier})", _state.Tier);
    }
}
