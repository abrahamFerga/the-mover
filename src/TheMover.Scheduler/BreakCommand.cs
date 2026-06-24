// TheMover.Scheduler — ARCH.md: API surface / BreakCommand channel
namespace TheMover.Scheduler;

public abstract record BreakCommand;

// Tier carries the break that was actually shown (state.Tier is already the NEXT break by the time the user acts).
// IsCompletion = true when the overlay timer expired naturally — handler skips the Dismissed log in that case.
public sealed record SkipBreakCommand(BreakTier? Tier = null, bool IsCompletion = false) : BreakCommand;

// Source distinguishes overlay vs tray snooze in the event log.
public sealed record SnoozeBreakCommand(int Minutes, string? Source = null) : BreakCommand;
