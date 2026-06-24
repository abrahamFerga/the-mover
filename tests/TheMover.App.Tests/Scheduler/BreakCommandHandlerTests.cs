// TheMover.App.Tests — BreakCommandHandlerService snooze / skip logic
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheMover.App.Config;
using TheMover.App.Logging;
using TheMover.App.Scheduler;
using TheMover.Scheduler;

namespace TheMover.App.Tests.Scheduler;

public sealed class BreakCommandHandlerTests
{
    private static (BreakCommandHandlerService Handler, Channel<BreakCommand> Commands, BreakTimerState State) Build()
    {
        var commands = Channel.CreateUnbounded<BreakCommand>();
        var state = new BreakTimerState();
        var settings = new AppSettings { Snooze = new SnoozeSettings { IncrementMinutes = 5 } };
        var handler = new BreakCommandHandlerService(
            commands,
            state,
            new EventLogger(NullLogger<EventLogger>.Instance),
            new OptionsMonitorStub(settings),
            NullLogger<BreakCommandHandlerService>.Instance);
        return (handler, commands, state);
    }

    [Fact]
    public async Task Snooze_SetsSnoozedUntil()
    {
        var (handler, commands, state) = Build();
        commands.Writer.TryWrite(new SnoozeBreakCommand(5));
        commands.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        // Let the handler consume the command
        await Task.Delay(50, cts.Token).ContinueWith(_ => { });

        Assert.NotNull(state.SnoozedUntil);
        Assert.True(state.SnoozedUntil > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Snooze_CausesIsPaused()
    {
        var (handler, commands, state) = Build();
        commands.Writer.TryWrite(new SnoozeBreakCommand(5));
        commands.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token).ContinueWith(_ => { });

        Assert.True(state.IsPaused);
    }

    [Fact]
    public async Task Skip_ResetsLastBreakTimestamps()
    {
        var (handler, commands, state) = Build();
        var longAgo = DateTimeOffset.UtcNow.AddHours(-3);
        state.LastMicroBreakAt = longAgo;
        state.LastLongBreakAt = longAgo;
        state.Tier = BreakTier.Long;

        commands.Writer.TryWrite(new SkipBreakCommand());
        commands.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token).ContinueWith(_ => { });

        Assert.True(state.LastMicroBreakAt > longAgo);
        Assert.True(state.LastLongBreakAt > longAgo);
    }

    [Fact]
    public async Task Skip_ClearsSnoozedUntil()
    {
        var (handler, commands, state) = Build();
        state.SnoozedUntil = DateTimeOffset.UtcNow.AddMinutes(5);

        commands.Writer.TryWrite(new SkipBreakCommand());
        commands.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token).ContinueWith(_ => { });

        Assert.Null(state.SnoozedUntil);
    }

    // Snoozing for N minutes should shift the break timestamps so the reminder
    // re-fires exactly N minutes later — not a full interval later.
    [Fact]
    public async Task Snooze_ShiftsTimestampsForReFire()
    {
        var commands = Channel.CreateUnbounded<BreakCommand>();
        var state = new BreakTimerState();
        var settings = new AppSettings
        {
            MicroBreak = new BreakTierSettings { IntervalMinutes = 20, DurationSeconds = 30 },
            LongBreak  = new BreakTierSettings { IntervalMinutes = 60, DurationSeconds = 300 },
            Snooze = new SnoozeSettings { IncrementMinutes = 5 }
        };
        var handler = new BreakCommandHandlerService(
            commands,
            state,
            new EventLogger(NullLogger<EventLogger>.Instance),
            new OptionsMonitorStub(settings),
            NullLogger<BreakCommandHandlerService>.Instance);

        commands.Writer.TryWrite(new SnoozeBreakCommand(5));
        commands.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token).ContinueWith(_ => { });

        // 5 minutes after the snooze is issued, the micro interval (20 min) must have elapsed
        // relative to LastMicroBreakAt so the scheduler fires immediately on snooze expiry.
        var microElapsedAtExpiry = (state.SnoozedUntil!.Value - state.LastMicroBreakAt).TotalMinutes;
        Assert.Equal(20.0, microElapsedAtExpiry, precision: 0); // within rounding
    }

    private sealed class OptionsMonitorStub(AppSettings value) : IOptionsMonitor<AppSettings>
    {
        public AppSettings CurrentValue => value;
        public AppSettings Get(string? name) => value;
        public IDisposable? OnChange(Action<AppSettings, string?> listener) => null;
    }
}
