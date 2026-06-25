// TheMover.App.Tests — BreakCommandHandlerService snooze / skip logic
using System.IO;
using System.Text.Json;
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

    [Fact]
    public async Task Snooze_LogsSourceFromCommand()
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"snooze-{Guid.NewGuid():N}.jsonl");
        try
        {
            var commands = Channel.CreateUnbounded<BreakCommand>();
            var state = new BreakTimerState();
            var settings = new AppSettings
            {
                MicroBreak = new BreakTierSettings { IntervalMinutes = 20, DurationSeconds = 30 },
                LongBreak = new BreakTierSettings { IntervalMinutes = 60, DurationSeconds = 300 },
                Snooze = new SnoozeSettings { IncrementMinutes = 5 }
            };
            var handler = new BreakCommandHandlerService(
                commands, state,
                new EventLogger(logPath, NullLogger<EventLogger>.Instance),
                new OptionsMonitorStub(settings),
                NullLogger<BreakCommandHandlerService>.Instance);

            commands.Writer.TryWrite(new SnoozeBreakCommand(5, Source: "overlay"));
            commands.Writer.Complete();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await handler.StartAsync(cts.Token);
            await Task.Delay(100, cts.Token).ContinueWith(_ => { });

            var log = File.ReadAllText(logPath);
            Assert.Contains("\"event\":\"Snoozed\"", log);
            Assert.Contains("\"source\":\"overlay\"", log);
            Assert.Contains("\"minutes\":5", log);
        }
        finally { if (File.Exists(logPath)) File.Delete(logPath); }
    }

    [Fact]
    public async Task Snooze_WithNoSource_LogsTraySource()
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"snooze-tray-{Guid.NewGuid():N}.jsonl");
        try
        {
            var commands = Channel.CreateUnbounded<BreakCommand>();
            var state = new BreakTimerState();
            var settings = new AppSettings
            {
                MicroBreak = new BreakTierSettings { IntervalMinutes = 20, DurationSeconds = 30 },
                LongBreak = new BreakTierSettings { IntervalMinutes = 60, DurationSeconds = 300 },
                Snooze = new SnoozeSettings { IncrementMinutes = 5 }
            };
            var handler = new BreakCommandHandlerService(
                commands, state,
                new EventLogger(logPath, NullLogger<EventLogger>.Instance),
                new OptionsMonitorStub(settings),
                NullLogger<BreakCommandHandlerService>.Instance);

            // No Source = tray default
            commands.Writer.TryWrite(new SnoozeBreakCommand(5));
            commands.Writer.Complete();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await handler.StartAsync(cts.Token);
            await Task.Delay(100, cts.Token).ContinueWith(_ => { });

            var log = File.ReadAllText(logPath);
            Assert.Contains("\"source\":\"tray\"", log);
        }
        finally { if (File.Exists(logPath)) File.Delete(logPath); }
    }

    [Fact]
    public async Task Skip_LogsDismissedWithTierFromCommand_NotFromState()
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"skip-{Guid.NewGuid():N}.jsonl");
        try
        {
            var commands = Channel.CreateUnbounded<BreakCommand>();
            var state = new BreakTimerState { Tier = BreakTier.Micro }; // state says Micro (next break)
            var settings = new AppSettings { Snooze = new SnoozeSettings { IncrementMinutes = 5 } };
            var handler = new BreakCommandHandlerService(
                commands, state,
                new EventLogger(logPath, NullLogger<EventLogger>.Instance),
                new OptionsMonitorStub(settings),
                NullLogger<BreakCommandHandlerService>.Instance);

            // But the actual break that was shown was Long
            commands.Writer.TryWrite(new SkipBreakCommand(BreakTier.Long));
            commands.Writer.Complete();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await handler.StartAsync(cts.Token);
            await Task.Delay(100, cts.Token).ContinueWith(_ => { });

            var log = File.ReadAllText(logPath);
            Assert.Contains("\"event\":\"Dismissed\"", log);
            Assert.Contains("\"tier\":\"Long\"", log);
            Assert.DoesNotContain("\"tier\":\"Micro\"", log);
        }
        finally { if (File.Exists(logPath)) File.Delete(logPath); }
    }

    [Fact]
    public async Task Skip_WithIsCompletion_DoesNotLogDismissed()
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"complete-{Guid.NewGuid():N}.jsonl");
        try
        {
            var commands = Channel.CreateUnbounded<BreakCommand>();
            var state = new BreakTimerState();
            var settings = new AppSettings { Snooze = new SnoozeSettings { IncrementMinutes = 5 } };
            var handler = new BreakCommandHandlerService(
                commands, state,
                new EventLogger(logPath, NullLogger<EventLogger>.Instance),
                new OptionsMonitorStub(settings),
                NullLogger<BreakCommandHandlerService>.Instance);

            commands.Writer.TryWrite(new SkipBreakCommand(BreakTier.Micro, IsCompletion: true));
            commands.Writer.Complete();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await handler.StartAsync(cts.Token);
            await Task.Delay(100, cts.Token).ContinueWith(_ => { });

            // Natural completion: no Dismissed event, timers reset
            Assert.False(File.Exists(logPath), "No event log should be written for natural completion");
            Assert.True(state.LastMicroBreakAt > DateTimeOffset.UtcNow.AddSeconds(-5));
        }
        finally { if (File.Exists(logPath)) File.Delete(logPath); }
    }

    // SnoozeBreakCommand with minutes <= 0 or > 1440 must be silently ignored —
    // no state change, no event logged.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1441)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public async Task Snooze_WithInvalidMinutes_IsIgnored(int invalidMinutes)
    {
        var (handler, commands, state) = Build();
        var before = state.SnoozedUntil;
        var beforeMicro = state.LastMicroBreakAt;

        commands.Writer.TryWrite(new SnoozeBreakCommand(invalidMinutes));
        commands.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token).ContinueWith(_ => { });

        Assert.Equal(before, state.SnoozedUntil);       // no snooze set
        Assert.Equal(beforeMicro, state.LastMicroBreakAt); // timestamps unchanged
    }

    // After a skip/completion the tray countdown must show the next break time, not a
    // stale fire timestamp. MicroBreak interval is used because both timers are reset.
    [Fact]
    public async Task Skip_SetsNextBreakAtToMicroInterval()
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
            commands, state,
            new EventLogger(NullLogger<EventLogger>.Instance),
            new OptionsMonitorStub(settings),
            NullLogger<BreakCommandHandlerService>.Instance);

        commands.Writer.TryWrite(new SkipBreakCommand(BreakTier.Micro));
        commands.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token).ContinueWith(_ => { });

        var expectedNext = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(20);
        Assert.True(Math.Abs((state.NextBreakAt - expectedNext).TotalSeconds) < 5,
            "NextBreakAt must equal now + micro interval after a skip");
    }

    // After a snooze the tray countdown must show the snooze expiry time, not a
    // stale pre-snooze NextBreakAt (SyncNextBreak won't run while the scheduler is paused).
    [Fact]
    public async Task Snooze_SetsNextBreakAtToSnoozeExpiry()
    {
        var (handler, commands, state) = Build();

        commands.Writer.TryWrite(new SnoozeBreakCommand(5));
        commands.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token).ContinueWith(_ => { });

        Assert.NotNull(state.SnoozedUntil);
        Assert.True(Math.Abs((state.NextBreakAt - state.SnoozedUntil!.Value).TotalSeconds) < 2,
            "NextBreakAt should equal SnoozedUntil so the tray shows the snooze expiry countdown");
    }

    // Snoozing a long break must shift LastLongBreakAt so the long break re-fires at
    // snooze expiry.  Without the Tier field, HandleSnooze sees ~0 ms elapsed since
    // LastLongBreakAt (the scheduler just reset it) and skips the shift, causing the
    // long break to fire 60 min later instead of at snooze expiry.
    [Fact]
    public async Task Snooze_WithLongTier_ShiftsLongBreakTimerToExpiry()
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
            commands, state,
            new EventLogger(NullLogger<EventLogger>.Instance),
            new OptionsMonitorStub(settings),
            NullLogger<BreakCommandHandlerService>.Instance);

        // Simulate the scheduler having just fired and reset LastLongBreakAt to now.
        state.LastLongBreakAt = DateTimeOffset.UtcNow;
        state.LastMicroBreakAt = DateTimeOffset.UtcNow;

        commands.Writer.TryWrite(new SnoozeBreakCommand(5, BreakTier.Long));
        commands.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token).ContinueWith(_ => { });

        // At snooze expiry the elapsed time since LastLongBreakAt must be ~60 min
        // so the scheduler fires the long break (not just a micro break).
        var longElapsedAtExpiry = (state.SnoozedUntil!.Value - state.LastLongBreakAt).TotalMinutes;
        Assert.True(Math.Abs(longElapsedAtExpiry - 60.0) < 1,
            $"Long-break timer must shift to expiry when tier is Long; elapsed = {longElapsedAtExpiry:F1} min");
    }

    // Snoozng a micro break must not advance the long-break timer when the long break
    // is not yet due.  The original handler always set LastLongBreakAt = now - longInterval
    // + snooze regardless, which caused a spurious long break at snooze expiry whenever
    // the micro break fired early in a long-break cycle (e.g. T=20 into a 60-min cycle).
    [Fact]
    public async Task Snooze_WhenLongBreakNotDue_DoesNotShiftLongBreakTimer()
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
            commands, state,
            new EventLogger(NullLogger<EventLogger>.Instance),
            new OptionsMonitorStub(settings),
            NullLogger<BreakCommandHandlerService>.Instance);

        // Last long break was 20 min ago → 40 min remain before the next one.
        state.LastLongBreakAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        commands.Writer.TryWrite(new SnoozeBreakCommand(5));
        commands.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token).ContinueWith(_ => { });

        // At snooze expiry the elapsed time since LastLongBreakAt must be < 60 min.
        // If >= 60, the scheduler would fire a premature long break instead of micro.
        var longElapsedAtExpiry = (state.SnoozedUntil!.Value - state.LastLongBreakAt).TotalMinutes;
        Assert.True(longElapsedAtExpiry < 60,
            $"Long-break timer must not shift when long break is not due; elapsed at expiry = {longElapsedAtExpiry:F1} min");
    }

    // Completing a micro break must not reset LastLongBreakAt — otherwise the long
    // break never fires for users who take every micro break (accumulated long-break
    // time is wiped on each micro completion, keeping elapsed < longInterval forever).
    [Fact]
    public async Task Skip_WithMicroTier_DoesNotResetLongBreakTimer()
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
            commands, state,
            new EventLogger(NullLogger<EventLogger>.Instance),
            new OptionsMonitorStub(settings),
            NullLogger<BreakCommandHandlerService>.Instance);

        // 40 min into a 60-min long-break cycle — 20 min remain before the long break.
        state.LastLongBreakAt = DateTimeOffset.UtcNow.AddMinutes(-40);

        commands.Writer.TryWrite(new SkipBreakCommand(BreakTier.Micro, IsCompletion: true));
        commands.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token).ContinueWith(_ => { });

        // The long-break timer must still show ~40 min elapsed, not 0 (reset).
        var longElapsed = (DateTimeOffset.UtcNow - state.LastLongBreakAt).TotalMinutes;
        Assert.True(longElapsed >= 39,
            $"LastLongBreakAt must not be reset on micro completion; elapsed = {longElapsed:F1} min");
    }

    // Completing or skipping a long break must reset LastLongBreakAt so the next
    // long break fires a full interval later.
    [Fact]
    public async Task Skip_WithLongTier_ResetsLongBreakTimer()
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
            commands, state,
            new EventLogger(NullLogger<EventLogger>.Instance),
            new OptionsMonitorStub(settings),
            NullLogger<BreakCommandHandlerService>.Instance);

        state.LastLongBreakAt = DateTimeOffset.UtcNow.AddMinutes(-60);

        commands.Writer.TryWrite(new SkipBreakCommand(BreakTier.Long, IsCompletion: true));
        commands.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await handler.StartAsync(cts.Token);
        await Task.Delay(50, cts.Token).ContinueWith(_ => { });

        var longElapsed = (DateTimeOffset.UtcNow - state.LastLongBreakAt).TotalSeconds;
        Assert.True(longElapsed < 5,
            $"LastLongBreakAt must be reset after long-break completion; elapsed = {longElapsed:F1} s");
    }

    private sealed class OptionsMonitorStub(AppSettings value) : IOptionsMonitor<AppSettings>
    {
        public AppSettings CurrentValue => value;
        public AppSettings Get(string? name) => value;
        public IDisposable? OnChange(Action<AppSettings, string?> listener) => null;
    }
}
