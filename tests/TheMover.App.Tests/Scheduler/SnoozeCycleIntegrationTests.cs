// TheMover.App.Tests — end-to-end: snooze command → scheduler re-fires after the delay
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheMover.App.Config;
using TheMover.App.Logging;
using TheMover.App.Scheduler;
using TheMover.Scheduler;

namespace TheMover.App.Tests.Scheduler;

public sealed class SnoozeCycleIntegrationTests
{
    // Verifies that after a snooze the scheduler fires at the snooze boundary.
    // IsPaused uses DateTimeOffset.UtcNow, so we backdate SnoozedUntil to simulate
    // expiry, and derive the "effective handler time" from state.SnoozedUntil so
    // the synthetic 'now' fed to CheckAndFireAsync aligns with LastMicroBreakAt.
    [Fact]
    public async Task AfterSnooze_SchedulerFiresWhenSnoozeExpires()
    {
        const int microMinutes = 20;
        const int snoozeMinutes = 5;

        var settings = new AppSettings
        {
            MicroBreak = new BreakTierSettings { IntervalMinutes = microMinutes, DurationSeconds = 30 },
            LongBreak  = new BreakTierSettings { IntervalMinutes = 60, DurationSeconds = 300 },
            Snooze = new SnoozeSettings { IncrementMinutes = snoozeMinutes }
        };
        var options = new OptionsMonitorStub(settings);
        var state = new BreakTimerState();
        var breakDueChannel = Channel.CreateBounded<BreakDueEvent>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true
        });
        var commandChannel = Channel.CreateUnbounded<BreakCommand>(
            new UnboundedChannelOptions { SingleReader = true });

        var handler = new BreakCommandHandlerService(
            commandChannel, state,
            new EventLogger(NullLogger<EventLogger>.Instance),
            options, NullLogger<BreakCommandHandlerService>.Instance);

        commandChannel.Writer.TryWrite(new SnoozeBreakCommand(snoozeMinutes));
        commandChannel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        await Task.Delay(80, cts.Token).ContinueWith(_ => { });

        // The handler set SnoozedUntil = handlerNow + 5 min.
        // Derive handlerNow from SnoozedUntil to align the synthetic clock below.
        Assert.NotNull(state.SnoozedUntil);
        var handlerNow = state.SnoozedUntil!.Value - TimeSpan.FromMinutes(snoozeMinutes);

        // Expire the snooze so IsPaused (which uses UtcNow) returns false.
        state.SnoozedUntil = DateTimeOffset.UtcNow.AddSeconds(-1);

        var scheduler = new BreakSchedulerService(
            options, breakDueChannel, state,
            NullLogger<BreakSchedulerService>.Instance);

        // At "handlerNow + 5 min" the handler-shifted LastMicroBreakAt satisfies:
        // (handlerNow + 5) − (handlerNow − 15) = 20 min ≥ microInterval → fires.
        var atExpiry = handlerNow + TimeSpan.FromMinutes(snoozeMinutes);
        var firedAtExpiry = await scheduler.CheckAndFireAsync(atExpiry);
        Assert.True(firedAtExpiry, "Scheduler must fire immediately when snooze expires");
    }

    [Fact]
    public async Task AfterSnooze_SchedulerDoesNotFireBeforeSnoozeExpires()
    {
        const int microMinutes = 20;
        const int snoozeMinutes = 5;

        var settings = new AppSettings
        {
            MicroBreak = new BreakTierSettings { IntervalMinutes = microMinutes, DurationSeconds = 30 },
            LongBreak  = new BreakTierSettings { IntervalMinutes = 60, DurationSeconds = 300 },
            Snooze = new SnoozeSettings { IncrementMinutes = snoozeMinutes }
        };
        var options = new OptionsMonitorStub(settings);
        var state = new BreakTimerState();
        var breakDueChannel = Channel.CreateBounded<BreakDueEvent>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true
        });
        var commandChannel = Channel.CreateUnbounded<BreakCommand>(
            new UnboundedChannelOptions { SingleReader = true });

        var handler = new BreakCommandHandlerService(
            commandChannel, state,
            new EventLogger(NullLogger<EventLogger>.Instance),
            options, NullLogger<BreakCommandHandlerService>.Instance);

        commandChannel.Writer.TryWrite(new SnoozeBreakCommand(snoozeMinutes));
        commandChannel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        await Task.Delay(80, cts.Token).ContinueWith(_ => { });

        Assert.NotNull(state.SnoozedUntil);
        var handlerNow = state.SnoozedUntil!.Value - TimeSpan.FromMinutes(snoozeMinutes);

        // Expire the snooze for IsPaused, then check 30 s before the full interval.
        state.SnoozedUntil = DateTimeOffset.UtcNow.AddSeconds(-1);

        var scheduler = new BreakSchedulerService(
            options, breakDueChannel, state,
            NullLogger<BreakSchedulerService>.Instance);

        var beforeExpiry = handlerNow + TimeSpan.FromMinutes(snoozeMinutes) - TimeSpan.FromSeconds(30);
        var firedBefore = await scheduler.CheckAndFireAsync(beforeExpiry);
        Assert.False(firedBefore, "Scheduler must not fire before the snooze delay has fully elapsed");
    }

    private sealed class OptionsMonitorStub(AppSettings value) : IOptionsMonitor<AppSettings>
    {
        public AppSettings CurrentValue => value;
        public AppSettings Get(string? name) => value;
        public IDisposable? OnChange(Action<AppSettings, string?> listener) => null;
    }
}
