// TheMover.App — ARCH.md: Data model / AppSettings (persisted — appsettings.local.json)
using System.ComponentModel.DataAnnotations;

namespace TheMover.App.Config;

public sealed class AppSettings : IValidatableObject
{
    public BreakTierSettings MicroBreak { get; set; } = new() { IntervalMinutes = 20, DurationSeconds = 30 };
    public BreakTierSettings LongBreak { get; set; } = new() { IntervalMinutes = 60, DurationSeconds = 300 };
    public bool AutoStartWithWindows { get; set; } = false;
    public SnoozeSettings Snooze { get; set; } = new();
    public CalendarSettings Calendar { get; set; } = new();

    // Cross-field constraint: micro must fire more often than long, otherwise
    // the long timer fires first on every cycle and micro breaks never occur.
    // Enforced here at the model level (not only in SettingsWindow.TryParseSettings)
    // so direct edits to appsettings.local.json are caught by ValidateOnStart().
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MicroBreak.IntervalMinutes >= LongBreak.IntervalMinutes)
        {
            yield return new ValidationResult(
                "Micro-break interval must be shorter than the long-break interval.",
                [nameof(MicroBreak), nameof(LongBreak)]);
        }

        // A duration ≥ interval means the overlay is still visible when the next break
        // fires, producing an endless cycle; catch this even if the file is edited directly.
        if (MicroBreak.DurationSeconds >= MicroBreak.IntervalMinutes * 60)
        {
            yield return new ValidationResult(
                "Micro-break duration must be shorter than the micro-break interval.",
                [nameof(MicroBreak)]);
        }

        if (LongBreak.DurationSeconds >= LongBreak.IntervalMinutes * 60)
        {
            yield return new ValidationResult(
                "Long-break duration must be shorter than the long-break interval.",
                [nameof(LongBreak)]);
        }
    }
}

public sealed class BreakTierSettings
{
    [Range(1, 240)] public int IntervalMinutes { get; set; }
    [Range(10, 600)] public int DurationSeconds { get; set; }
}

public sealed class SnoozeSettings
{
    [Range(1, 30)] public int IncrementMinutes { get; set; } = 5;
}

public sealed class CalendarSettings
{
    public bool Enabled { get; set; } = false;
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
}
