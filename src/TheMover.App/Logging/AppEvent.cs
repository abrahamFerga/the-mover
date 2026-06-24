// TheMover.App — ARCH.md: Data model / Local event log
namespace TheMover.App.Logging;

public enum AppEventType
{
    BreakFired,
    OverlayShown,
    Snoozed,
    Dismissed,
    Heartbeat
}
