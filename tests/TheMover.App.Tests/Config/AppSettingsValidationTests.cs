// TheMover.App.Tests — AppSettings validation (ARCH.md: Config / ValidateDataAnnotations)
using System.ComponentModel.DataAnnotations;
using TheMover.App.Config;

namespace TheMover.App.Tests.Config;

public sealed class AppSettingsValidationTests
{
    [Fact]
    public void DefaultSettings_PassValidation()
    {
        var settings = new AppSettings();
        var results = new List<ValidationResult>();
        var ctx = new ValidationContext(settings);
        Assert.True(Validator.TryValidateObject(settings, ctx, results, validateAllProperties: true));
    }

    [Fact]
    public void MicroBreak_IntervalBelowRange_FailsValidation()
    {
        var tier = new BreakTierSettings { IntervalMinutes = 0, DurationSeconds = 30 };
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(tier, new ValidationContext(tier), results, validateAllProperties: true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(BreakTierSettings.IntervalMinutes)));
    }

    [Fact]
    public void MicroBreak_IntervalAboveRange_FailsValidation()
    {
        var tier = new BreakTierSettings { IntervalMinutes = 300, DurationSeconds = 30 };
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(tier, new ValidationContext(tier), results, validateAllProperties: true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(BreakTierSettings.IntervalMinutes)));
    }

    [Fact]
    public void Snooze_IncrementBelowRange_FailsValidation()
    {
        var snooze = new SnoozeSettings { IncrementMinutes = 0 };
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(snooze, new ValidationContext(snooze), results, validateAllProperties: true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SnoozeSettings.IncrementMinutes)));
    }

    [Fact]
    public void Snooze_IncrementAboveRange_FailsValidation()
    {
        var snooze = new SnoozeSettings { IncrementMinutes = 31 };
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(snooze, new ValidationContext(snooze), results, validateAllProperties: true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SnoozeSettings.IncrementMinutes)));
    }

    [Fact]
    public void Duration_BelowRange_FailsValidation()
    {
        var tier = new BreakTierSettings { IntervalMinutes = 20, DurationSeconds = 9 };
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(tier, new ValidationContext(tier), results, validateAllProperties: true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(BreakTierSettings.DurationSeconds)));
    }

    [Fact]
    public void Duration_AboveRange_FailsValidation()
    {
        var tier = new BreakTierSettings { IntervalMinutes = 20, DurationSeconds = 601 };
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(tier, new ValidationContext(tier), results, validateAllProperties: true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(BreakTierSettings.DurationSeconds)));
    }

    // Cross-field: micro interval must be strictly less than long interval.
    [Fact]
    public void Settings_WhenMicroEqualsLong_FailsValidation()
    {
        var settings = new AppSettings
        {
            MicroBreak = new BreakTierSettings { IntervalMinutes = 30, DurationSeconds = 30 },
            LongBreak  = new BreakTierSettings { IntervalMinutes = 30, DurationSeconds = 300 },
        };
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(settings, new ValidationContext(settings), results, validateAllProperties: true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AppSettings.MicroBreak)));
    }

    [Fact]
    public void Settings_WhenMicroGreaterThanLong_FailsValidation()
    {
        var settings = new AppSettings
        {
            MicroBreak = new BreakTierSettings { IntervalMinutes = 60, DurationSeconds = 30 },
            LongBreak  = new BreakTierSettings { IntervalMinutes = 20, DurationSeconds = 300 },
        };
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(settings, new ValidationContext(settings), results, validateAllProperties: true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AppSettings.MicroBreak)));
    }

    [Fact]
    public void Settings_WhenMicroLessThanLong_PassesValidation()
    {
        var settings = new AppSettings
        {
            MicroBreak = new BreakTierSettings { IntervalMinutes = 20, DurationSeconds = 30 },
            LongBreak  = new BreakTierSettings { IntervalMinutes = 60, DurationSeconds = 300 },
        };
        var results = new List<ValidationResult>();
        Assert.True(Validator.TryValidateObject(settings, new ValidationContext(settings), results, validateAllProperties: true));
    }
}
