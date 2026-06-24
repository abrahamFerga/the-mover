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
}
