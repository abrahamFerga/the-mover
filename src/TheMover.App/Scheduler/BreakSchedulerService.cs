// TheMover.App — ARCH.md: Components / BreakSchedulerService (fires BreakDueEvent to channel)
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheMover.App.Config;
using TheMover.Scheduler;

namespace TheMover.App.Scheduler;

public sealed class BreakSchedulerService : BackgroundService
{
    private readonly IOptionsMonitor<AppSettings> _options;
    private readonly Channel<BreakDueEvent> _channel;
    private readonly BreakTimerState _state;
    private readonly ILogger<BreakSchedulerService> _logger;

    public BreakSchedulerService(
        IOptionsMonitor<AppSettings> options,
        Channel<BreakDueEvent> channel,
        BreakTimerState state,
        ILogger<BreakSchedulerService> logger)
    {
        _options = options;
        _channel = channel;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SyncNextBreak();

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckAndFireAsync(DateTimeOffset.UtcNow);
        }
    }

    // Internal for unit testing without real timers
    internal async Task<bool> CheckAndFireAsync(DateTimeOffset now)
    {
        if (_state.IsPausedAt(now)) return false;

        var settings = _options.CurrentValue;
        var longInterval = TimeSpan.FromMinutes(settings.LongBreak.IntervalMinutes);
        var microInterval = TimeSpan.FromMinutes(settings.MicroBreak.IntervalMinutes);

        if (now - _state.LastLongBreakAt >= longInterval)
        {
            await FireAsync(BreakTier.Long, now);
            _state.LastLongBreakAt = now;
            _state.LastMicroBreakAt = now;
            SyncNextBreak();
            return true;
        }

        if (now - _state.LastMicroBreakAt >= microInterval)
        {
            await FireAsync(BreakTier.Micro, now);
            _state.LastMicroBreakAt = now;
            SyncNextBreak();
            return true;
        }

        // Keep NextBreakAt in sync every tick so the tray countdown reflects
        // any settings change made while the app is running.
        SyncNextBreak();
        return false;
    }

    private async Task FireAsync(BreakTier tier, DateTimeOffset firedAt)
    {
        _state.Tier = tier;
        _state.NextBreakAt = firedAt;
        await _channel.Writer.WriteAsync(new BreakDueEvent(tier, firedAt));
        _logger.LogInformation("Break due: {Tier}", tier);
    }

    // No 'now' needed — computes entirely from stored last-break timestamps and settings.
    private void SyncNextBreak()
    {
        var settings = _options.CurrentValue;
        var nextMicro = _state.LastMicroBreakAt + TimeSpan.FromMinutes(settings.MicroBreak.IntervalMinutes);
        var nextLong = _state.LastLongBreakAt + TimeSpan.FromMinutes(settings.LongBreak.IntervalMinutes);
        _state.NextBreakAt = nextMicro <= nextLong ? nextMicro : nextLong;
        _state.Tier = nextMicro <= nextLong ? BreakTier.Micro : BreakTier.Long;
    }
}
