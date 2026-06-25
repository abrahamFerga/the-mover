// TheMover.Scheduler — ARCH.md: Data model / BreakTimerState (in-memory; rebuilt on startup)
namespace TheMover.Scheduler;

public sealed class BreakTimerState
{
    public DateTimeOffset NextBreakAt { get; set; } = DateTimeOffset.MaxValue;
    public BreakTier Tier { get; set; } = BreakTier.Micro;
    // Set when the overlay opens (SyncNextBreak already overwrites Tier to the NEXT break
    // by the time the user can act, so we need a separate field for tray-side dismissal).
    public BreakTier? FiringTier { get; set; }
    public DateTimeOffset? IdleDetectedAt { get; set; }
    public bool HeldForMeeting { get; set; }
    public DateTimeOffset? SnoozedUntil { get; set; }

    // Shared with BreakSchedulerService so BreakCommandHandlerService can reset intervals
    public DateTimeOffset LastMicroBreakAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastLongBreakAt { get; set; } = DateTimeOffset.UtcNow;

    // Convenience property — delegates to IsPausedAt with the real clock.
    // Production code that doesn't have a synthetic 'now' can keep using this.
    public bool IsPaused => IsPausedAt(DateTimeOffset.UtcNow);

    // Clock-injectable variant used by BreakSchedulerService so that the same
    // 'now' value drives both the elapsed-time checks and the snooze expiry check.
    // Passing a controlled timestamp in tests lets callers verify precise snooze
    // boundaries without manipulating the real clock.
    public bool IsPausedAt(DateTimeOffset now) =>
        IdleDetectedAt.HasValue ||
        HeldForMeeting ||
        (SnoozedUntil.HasValue && SnoozedUntil.Value > now);
}
