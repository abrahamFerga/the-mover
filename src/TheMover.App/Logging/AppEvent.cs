// TheMover.App — ARCH.md: Data model / Local event log
namespace TheMover.App.Logging;

public enum AppEventType
{
    FirstRunCompleted,
    BreakFired,
    OverlayShown,
    BreakCompleted,
    Snoozed,
    Dismissed,
    Heartbeat
}
