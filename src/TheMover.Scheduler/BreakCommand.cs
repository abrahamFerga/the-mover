// TheMover.Scheduler — ARCH.md: API surface / BreakCommand channel
namespace TheMover.Scheduler;

public abstract record BreakCommand;
public sealed record SkipBreakCommand : BreakCommand;
public sealed record SnoozeBreakCommand(int Minutes) : BreakCommand;
