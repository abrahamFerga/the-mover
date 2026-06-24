// TheMover.App.Tests — BreakSchedulerService core scheduling logic
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheMover.App.Config;
using TheMover.App.Scheduler;
using TheMover.Scheduler;

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
    public async Task DoesNotFire_WhenPaused()
    {
        var state = new BreakTimerState { HeldForMeeting = true };
        var (svc, ch, _) = BuildService(state: state);
        state.LastMicroBreakAt = DateTimeOffset.UtcNow.AddMinutes(-25);

        var fired = await svc.CheckAndFireAsync(DateTimeOffset.UtcNow);

        Assert.False(fired);
        Assert.False(ch.Reader.TryRead(out _));
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

    private sealed class OptionsMonitorStub(AppSettings value) : IOptionsMonitor<AppSettings>
    {
        public AppSettings CurrentValue => value;
        public AppSettings Get(string? name) => value;
        public IDisposable? OnChange(Action<AppSettings, string?> listener) => null;
    }
}
