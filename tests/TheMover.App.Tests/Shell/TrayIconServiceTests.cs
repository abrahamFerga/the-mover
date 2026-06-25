// TheMover.App.Tests — TrayIconService.BuildActiveTooltipText pure-logic tests
using TheMover.App.Shell;

namespace TheMover.App.Tests.Shell;

public sealed class TrayIconServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WhenNextBreakAtIsMaxValue_ReturnsActiveMessage()
    {
        var text = TrayIconService.BuildActiveTooltipText(DateTimeOffset.MaxValue, Now);

        Assert.Equal("The Mover — Break reminder active", text);
    }

    [Fact]
    public void WhenBreakIsDue_ReturnsBreakDueMessage()
    {
        var nextBreakAt = Now.AddSeconds(-1); // already past

        var text = TrayIconService.BuildActiveTooltipText(nextBreakAt, Now);

        Assert.Equal("The Mover — Break due", text);
    }

    [Fact]
    public void WhenBreakExactlyNow_ReturnsBreakDueMessage()
    {
        var text = TrayIconService.BuildActiveTooltipText(Now, Now);

        Assert.Equal("The Mover — Break due", text);
    }

    [Fact]
    public void WhenBreakIn20Minutes_Returns20MinMessage()
    {
        var nextBreakAt = Now.AddMinutes(20);

        var text = TrayIconService.BuildActiveTooltipText(nextBreakAt, Now);

        Assert.Equal("The Mover — Next break in 20 min", text);
    }

    [Fact]
    public void WhenBreakIn1Minute30Seconds_CeilsTo2Minutes()
    {
        var nextBreakAt = Now.AddMinutes(1).AddSeconds(30);

        var text = TrayIconService.BuildActiveTooltipText(nextBreakAt, Now);

        Assert.Equal("The Mover — Next break in 2 min", text);
    }

    [Fact]
    public void WhenBreakIn1MinuteExact_Returns1MinMessage()
    {
        var nextBreakAt = Now.AddMinutes(1);

        var text = TrayIconService.BuildActiveTooltipText(nextBreakAt, Now);

        Assert.Equal("The Mover — Next break in 1 min", text);
    }

    [Fact]
    public void WhenBreakIn1Second_CeilsTo1Minute()
    {
        var nextBreakAt = Now.AddSeconds(1);

        var text = TrayIconService.BuildActiveTooltipText(nextBreakAt, Now);

        Assert.Equal("The Mover — Next break in 1 min", text);
    }
}
