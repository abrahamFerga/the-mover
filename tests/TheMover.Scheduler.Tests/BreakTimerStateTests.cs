// TheMover.Scheduler.Tests — BreakTimerState.IsPaused logic
using TheMover.Scheduler;

namespace TheMover.Scheduler.Tests;

public sealed class BreakTimerStateTests
{
    [Fact]
    public void IsPaused_DefaultState_IsNotPaused()
    {
        var state = new BreakTimerState();
        Assert.False(state.IsPaused);
    }

    [Fact]
    public void IsPaused_WhenIdleDetected_IsTrue()
    {
        var state = new BreakTimerState { IdleDetectedAt = DateTimeOffset.UtcNow };
        Assert.True(state.IsPaused);
    }

    [Fact]
    public void IsPaused_WhenHeldForMeeting_IsTrue()
    {
        var state = new BreakTimerState { HeldForMeeting = true };
        Assert.True(state.IsPaused);
    }

    [Fact]
    public void IsPaused_WhenSnoozedInFuture_IsTrue()
    {
        var state = new BreakTimerState { SnoozedUntil = DateTimeOffset.UtcNow.AddMinutes(5) };
        Assert.True(state.IsPaused);
    }

    [Fact]
    public void IsPaused_WhenSnoozedInPast_IsFalse()
    {
        var state = new BreakTimerState { SnoozedUntil = DateTimeOffset.UtcNow.AddMinutes(-1) };
        Assert.False(state.IsPaused);
    }

    [Fact]
    public void FiringTier_IsNullByDefault()
    {
        var state = new BreakTimerState();
        Assert.Null(state.FiringTier);
    }

    [Fact]
    public void FiringTier_CanBeSetAndCleared()
    {
        var state = new BreakTimerState();
        state.FiringTier = BreakTier.Long;
        Assert.Equal(BreakTier.Long, state.FiringTier);
        state.FiringTier = null;
        Assert.Null(state.FiringTier);
    }

    [Fact]
    public void NextBreakAt_DefaultsToMaxValue()
    {
        // TrayIconService guards against MaxValue to avoid int overflow in the countdown tooltip.
        var state = new BreakTimerState();
        Assert.Equal(DateTimeOffset.MaxValue, state.NextBreakAt);
    }

    [Fact]
    public void BreakDueEvent_HasCorrectProperties()
    {
        var firedAt = DateTimeOffset.UtcNow;
        var evt = new BreakDueEvent(BreakTier.Micro, firedAt);
        Assert.Equal(BreakTier.Micro, evt.Tier);
        Assert.Equal(firedAt, evt.FiredAt);
    }

    [Fact]
    public void BreakCommands_AreDistinctTypes()
    {
        BreakCommand skip = new SkipBreakCommand();
        BreakCommand snooze = new SnoozeBreakCommand(5);
        Assert.IsType<SkipBreakCommand>(skip);
        Assert.IsType<SnoozeBreakCommand>(snooze);
        Assert.Equal(5, ((SnoozeBreakCommand)snooze).Minutes);
    }
}
