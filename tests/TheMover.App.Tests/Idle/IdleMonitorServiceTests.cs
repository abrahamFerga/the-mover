// TheMover.App.Tests — IdleMonitorService idle detection / resume logic
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using TheMover.App.Idle;
using TheMover.Scheduler;

namespace TheMover.App.Tests.Idle;

public sealed class IdleMonitorServiceTests
{
    private static (IdleMonitorService Service, BreakTimerState State, List<bool> TooltipCalls) Build(
        TimeSpan idleTime)
    {
        var state = new BreakTimerState();
        var tooltipCalls = new List<bool>();
        var svc = new IdleMonitorService(
            state,
            () => idleTime,
            paused => tooltipCalls.Add(paused),
            NullLogger<IdleMonitorService>.Instance);
        return (svc, state, tooltipCalls);
    }

    [Fact]
    public void WhenIdle2Min_SetsIdleDetectedAt()
    {
        var (svc, state, _) = Build(TimeSpan.FromMinutes(2));

        svc.CheckIdle();

        Assert.NotNull(state.IdleDetectedAt);
    }

    [Fact]
    public void WhenIdle2Min_CausesIsPaused()
    {
        var (svc, state, _) = Build(TimeSpan.FromMinutes(2));

        svc.CheckIdle();

        Assert.True(state.IsPaused);
    }

    [Fact]
    public void WhenIdle2Min_UpdatesTooltipPaused()
    {
        var (svc, _, tooltipCalls) = Build(TimeSpan.FromMinutes(2));

        svc.CheckIdle();

        Assert.Contains(true, tooltipCalls);
    }

    [Fact]
    public void WhenBelowThreshold_DoesNotSetIdleDetectedAt()
    {
        var (svc, state, _) = Build(TimeSpan.FromSeconds(30));

        svc.CheckIdle();

        Assert.Null(state.IdleDetectedAt);
    }

    [Fact]
    public void WhenActivityResumesAfterIdle_ClearsIdleDetectedAt()
    {
        var idleTime = TimeSpan.FromMinutes(3);
        var state = new BreakTimerState();
        var svc = new IdleMonitorService(
            state,
            () => idleTime,
            _ => { },
            NullLogger<IdleMonitorService>.Instance);

        svc.CheckIdle(); // goes idle
        idleTime = TimeSpan.FromSeconds(1);
        svc.CheckIdle(); // activity resumes

        Assert.Null(state.IdleDetectedAt);
    }

    [Fact]
    public void WhenActivityResumes_ResetsBreakIntervals()
    {
        var idleTime = TimeSpan.FromMinutes(3);
        var state = new BreakTimerState();
        var longAgo = DateTimeOffset.UtcNow.AddHours(-2);
        state.LastMicroBreakAt = longAgo;
        state.LastLongBreakAt = longAgo;
        var svc = new IdleMonitorService(
            state,
            () => idleTime,
            _ => { },
            NullLogger<IdleMonitorService>.Instance);

        svc.CheckIdle(); // goes idle
        idleTime = TimeSpan.FromSeconds(1);
        svc.CheckIdle(); // activity resumes

        Assert.True(state.LastMicroBreakAt > longAgo);
        Assert.True(state.LastLongBreakAt > longAgo);
    }

    [Fact]
    public void WhenActivityResumes_UpdatesTooltipActive()
    {
        var idleTime = TimeSpan.FromMinutes(3);
        var tooltipCalls = new List<bool>();
        var state = new BreakTimerState();
        var svc = new IdleMonitorService(
            state,
            () => idleTime,
            paused => tooltipCalls.Add(paused),
            NullLogger<IdleMonitorService>.Instance);

        svc.CheckIdle();
        idleTime = TimeSpan.FromSeconds(1);
        svc.CheckIdle();

        Assert.Contains(false, tooltipCalls);
    }

    [Fact]
    public void IdleDetectedOnlyOnce_WhenAlreadyIdle()
    {
        var (svc, state, tooltipCalls) = Build(TimeSpan.FromMinutes(5));

        svc.CheckIdle();
        svc.CheckIdle(); // second call while still idle

        Assert.Single(tooltipCalls);
    }

    [Fact]
    public void OnSuspend_SetsIdleDetectedAtAndTooltipPaused()
    {
        var (svc, state, tooltipCalls) = Build(TimeSpan.Zero);

        svc.OnPowerModeChanged(this, new PowerModeChangedEventArgs(PowerModes.Suspend));

        Assert.NotNull(state.IdleDetectedAt);
        Assert.True(state.IsPaused);
        Assert.Contains(true, tooltipCalls);
    }

    [Fact]
    public void OnResume_ClearsIdleAndResetsBreakIntervals()
    {
        var state = new BreakTimerState
        {
            IdleDetectedAt = DateTimeOffset.UtcNow,
            LastMicroBreakAt = DateTimeOffset.UtcNow.AddHours(-3),
            LastLongBreakAt = DateTimeOffset.UtcNow.AddHours(-3)
        };
        var svc = new IdleMonitorService(state, () => TimeSpan.Zero, _ => { },
            NullLogger<IdleMonitorService>.Instance);

        svc.OnPowerModeChanged(this, new PowerModeChangedEventArgs(PowerModes.Resume));

        Assert.Null(state.IdleDetectedAt);
        Assert.False(state.IsPaused);
        Assert.True(state.LastMicroBreakAt > DateTimeOffset.UtcNow.AddSeconds(-5));
        Assert.True(state.LastLongBreakAt > DateTimeOffset.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public void OnResume_UpdatesTooltipActive()
    {
        var tooltipCalls = new List<bool>();
        var state = new BreakTimerState { IdleDetectedAt = DateTimeOffset.UtcNow };
        var svc = new IdleMonitorService(state, () => TimeSpan.Zero,
            paused => tooltipCalls.Add(paused), NullLogger<IdleMonitorService>.Instance);

        svc.OnPowerModeChanged(this, new PowerModeChangedEventArgs(PowerModes.Resume));

        Assert.Contains(false, tooltipCalls);
    }
}
