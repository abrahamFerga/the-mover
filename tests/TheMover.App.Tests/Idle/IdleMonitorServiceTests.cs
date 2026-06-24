// TheMover.App.Tests — IdleMonitorService idle detection / resume logic
using Microsoft.Extensions.Logging.Abstractions;
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
}
