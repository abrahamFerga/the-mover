// TheMover.Scheduler — ARCH.md: API surface / Internal event channel
namespace TheMover.Scheduler;

public enum BreakTier { Micro, Long }

public sealed record BreakDueEvent(BreakTier Tier, DateTimeOffset FiredAt, Guid ExerciseId);
