// TheMover.App.Tests — BreakSchedulerService core scheduling logic
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheMover.App.Config;
using TheMover.App.Scheduler;
using TheMover.Scheduler;
using TheMover.App.Tests;

namespace TheMover.App.Tests.Scheduler;

public sealed class BreakSchedulerServiceTests
{
    private static (BreakSchedulerService Service, Channel<BreakDueEvent> Channel, BreakTimerState State) BuildService(
        AppSettings? settings = null,
        BreakTimerState? state = null)
    {
        settings ??= new AppSettings
        {
            MicroBreak = new BreakTierSettings { IntervalMinutes = 20, DurationSeconds = 30 },
            LongBreak = new BreakTierSettings { IntervalMinutes = 60, DurationSeconds = 300 },
        };
        state ??= new BreakTimerState();
        var channel = Channel.CreateUnbounded<BreakDueEvent>();
        var svc = new BreakSchedulerService(
            new OptionsMonitorStub(settings),
            channel,
            state,
            NullLogger<BreakSchedulerService>.Instance);
        return (svc, channel, state);
    }

    [Fact]
    public async Task MicroBreak_FiresAfterMicroInterval()
    {
        var (svc, ch, state) = BuildService();
        state.LastMicroBreakAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        await svc.CheckAndFireAsync(DateTimeOffset.UtcNow);

        Assert.True(ch.Reader.TryRead(out var evt));
        Assert.Equal(BreakTier.Micro, evt.Tier);
    }

    [Fact]
    public async Task LongBreak_FiresAfterLongInterval_AndResetsMicroTimer()
    {
        var (svc, ch, state) = BuildService();
        var ago = DateTimeOffset.UtcNow.AddMinutes(-60);
        state.LastLongBreakAt = ago;
        state.LastMicroBreakAt = ago;

        await svc.CheckAndFireAsync(DateTimeOffset.UtcNow);

        Assert.True(ch.Reader.TryRead(out var evt));
        Assert.Equal(BreakTier.Long, evt.Tier);
        Assert.False(ch.Reader.TryRead(out _));
    }

    [Fact]
    public async Task DoesNotFire_WhenNotEnoughTimeElapsed()
    {
        var (svc, ch, _) = BuildService();

        var fired = await svc.CheckAndFireAsync(DateTimeOffset.UtcNow);

        Assert.False(fired);
        Assert.False(ch.Reader.TryRead(out _));
    }

    [Fact]
    public async Task DoesNotFire_WhenPaused_HeldForMeeting()
    {
        var state = new BreakTimerState { HeldForMeeting = true };
        var (svc, ch, _) = BuildService(state: state);
        state.LastMicroBreakAt = DateTimeOffset.UtcNow.AddMinutes(-25);

        var fired = await svc.CheckAndFireAsync(DateTimeOffset.UtcNow);

        Assert.False(fired);
        Assert.False(ch.Reader.TryRead(out _));
    }

    [Fact]
    public async Task DoesNotFire_WhenPaused_IdleDetected()
    {
        var state = new BreakTimerState { IdleDetectedAt = DateTimeOffset.UtcNow.AddMinutes(-5) };
        var (svc, ch, _) = BuildService(state: state);
        state.LastMicroBreakAt = DateTimeOffset.UtcNow.AddMinutes(-25);

        var fired = await svc.CheckAndFireAsync(DateTimeOffset.UtcNow);

        Assert.False(fired);
        Assert.False(ch.Reader.TryRead(out _));
    }

    [Fact]
    public async Task DoesNotFire_WhenPaused_Snoozed()
    {
        var state = new BreakTimerState { SnoozedUntil = DateTimeOffset.UtcNow.AddMinutes(3) };
        var (svc, ch, _) = BuildService(state: state);
        state.LastMicroBreakAt = DateTimeOffset.UtcNow.AddMinutes(-25);

        var fired = await svc.CheckAndFireAsync(DateTimeOffset.UtcNow);

        Assert.False(fired);
        Assert.False(ch.Reader.TryRead(out _));
    }

    // When both intervals elapsed simultaneously, Tier should be Micro after reset
    // (SyncNextBreak: nextMicro <= nextLong → Micro wins the tie).
    [Fact]
    public async Task SyncNextBreak_WhenTied_PicksMicro()
    {
        var state = new BreakTimerState();
        var (svc, _, _) = BuildService(state: state);
        var now = DateTimeOffset.UtcNow;

        // Set both to the same "last break" so nextMicro == nextLong after the long fires and resets both
        var ago = now.AddMinutes(-60);
        state.LastMicroBreakAt = ago;
        state.LastLongBreakAt = ago;

        await svc.CheckAndFireAsync(now); // long fires, resets both to now

        // After reset, nextMicro = now+20, nextLong = now+60 → Micro
        Assert.Equal(BreakTier.Micro, state.Tier);
    }

    [Fact]
    public async Task LongBreak_TakesPriorityOverMicro_WhenBothElapsed()
    {
        var (svc, ch, state) = BuildService();
        var ago = DateTimeOffset.UtcNow.AddHours(-2);
        state.LastMicroBreakAt = ago;
        state.LastLongBreakAt = ago;

        await svc.CheckAndFireAsync(DateTimeOffset.UtcNow);

        Assert.True(ch.Reader.TryRead(out var evt));
        Assert.Equal(BreakTier.Long, evt.Tier);
    }

    [Fact]
    public async Task NextBreakAt_UpdatedAfterMicroFire()
    {
        var state = new BreakTimerState();
        var (svc, _, _) = BuildService(state: state);
        var now = DateTimeOffset.UtcNow;
        state.LastMicroBreakAt = now.AddMinutes(-20);

        await svc.CheckAndFireAsync(now);

        var expectedNext = now + TimeSpan.FromMinutes(20);
        Assert.True(Math.Abs((state.NextBreakAt - expectedNext).TotalSeconds) < 5);
    }

    [Fact]
    public async Task NextBreakAt_UpdatedToMicroIntervalAfterLongBreakFires()
    {
        var state = new BreakTimerState();
        var (svc, _, _) = BuildService(state: state);
        var now = DateTimeOffset.UtcNow;
        var ago = now.AddMinutes(-60);
        state.LastLongBreakAt = ago;
        state.LastMicroBreakAt = ago;

        await svc.CheckAndFireAsync(now);

        // After a long break, both timers reset to now; next break is micro in 20 min
        var expectedNext = now + TimeSpan.FromMinutes(20);
        Assert.True(Math.Abs((state.NextBreakAt - expectedNext).TotalSeconds) < 5);
        Assert.Equal(BreakTier.Micro, state.Tier);
    }

    // NextBreakAt must reflect the CURRENT setting, not the value at last fire.
    // Regression guard: if SyncNextBreak is only called on fire, a settings change
    // leaves the tray countdown showing a stale time until the next break fires.
    [Fact]
    public async Task NextBreakAt_UpdatesOnNextTickAfterSettingsChange()
    {
        var settings = new AppSettings
        {
            MicroBreak = new BreakTierSettings { IntervalMinutes = 20, DurationSeconds = 30 },
            LongBreak = new BreakTierSettings { IntervalMinutes = 60, DurationSeconds = 300 }
        };
        var state = new BreakTimerState();
        var stub = new MutableOptionsMonitorStub(settings);
        var channel = Channel.CreateUnbounded<BreakDueEvent>();
        var svc = new BreakSchedulerService(stub, channel, state, NullLogger<BreakSchedulerService>.Instance);

        var now = DateTimeOffset.UtcNow;
        await svc.CheckAndFireAsync(now); // syncs NextBreakAt with 20-min interval

        var nextBefore = state.NextBreakAt; // roughly now + 20 min

        // User changes interval to 10 minutes in Settings
        stub.Value = new AppSettings
        {
            MicroBreak = new BreakTierSettings { IntervalMinutes = 10, DurationSeconds = 30 },
            LongBreak = new BreakTierSettings { IntervalMinutes = 60, DurationSeconds = 300 }
        };

        await svc.CheckAndFireAsync(now); // no fire, but should re-sync

        Assert.True(state.NextBreakAt < nextBefore, "NextBreakAt must shrink when interval is reduced");
    }

    private sealed class MutableOptionsMonitorStub(AppSettings initial) : IOptionsMonitor<AppSettings>
    {
        public AppSettings Value = initial;
        public AppSettings CurrentValue => Value;
        public AppSettings Get(string? name) => Value;
        public IDisposable? OnChange(Action<AppSettings, string?> listener) => null;
    }

}
