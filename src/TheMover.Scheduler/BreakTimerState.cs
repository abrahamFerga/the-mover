// TheMover.Scheduler — ARCH.md: Data model / BreakTimerState (in-memory; rebuilt on startup)
namespace TheMover.Scheduler;

public sealed class BreakTimerState
{
    public DateTimeOffset NextBreakAt { get; set; } = DateTimeOffset.MaxValue;
    public BreakTier Tier { get; set; } = BreakTier.Micro;
    public DateTimeOffset? IdleDetectedAt { get; set; }
    public bool HeldForMeeting { get; set; }
    public DateTimeOffset? SnoozedUntil { get; set; }

    // Shared with BreakSchedulerService so BreakCommandHandlerService can reset intervals
    public DateTimeOffset LastMicroBreakAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastLongBreakAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsPaused =>
        IdleDetectedAt.HasValue ||
        HeldForMeeting ||
        (SnoozedUntil.HasValue && SnoozedUntil.Value > DateTimeOffset.UtcNow);
}
