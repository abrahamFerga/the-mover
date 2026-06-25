// TheMover.App.Tests — ConfigManager save/load round-trip
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheMover.App.Config;

namespace TheMover.App.Tests.Config;

public sealed class ConfigManagerTests
{
    private static ConfigManager BuildWithTempPath(string path)
    {
        var stub = new OptionsMonitorStub(new AppSettings());
        return new ConfigManager(stub, NullLogger<ConfigManager>.Instance) { ConfigPath = path };
    }

    [Fact]
    public async Task SaveAsync_WritesValidJson()
    {
        var path = TempPath();
        try
        {
            var settings = new AppSettings
            {
                MicroBreak = new BreakTierSettings { IntervalMinutes = 15, DurationSeconds = 45 },
                LongBreak = new BreakTierSettings { IntervalMinutes = 90, DurationSeconds = 300 },
            };
            await BuildWithTempPath(path).SaveAsync(settings);

            var json = await File.ReadAllTextAsync(path);
            var doc = JsonDocument.Parse(json); // throws if invalid JSON
            Assert.NotNull(doc);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task SaveAsync_RoundTrips_MicroBreakSettings()
    {
        var path = TempPath();
        try
        {
            var settings = new AppSettings
            {
                MicroBreak = new BreakTierSettings { IntervalMinutes = 15, DurationSeconds = 45 },
                LongBreak = new BreakTierSettings { IntervalMinutes = 90, DurationSeconds = 300 },
            };
            await BuildWithTempPath(path).SaveAsync(settings);

            var json = await File.ReadAllTextAsync(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json)!;
            Assert.Equal(15, loaded.MicroBreak.IntervalMinutes);
            Assert.Equal(45, loaded.MicroBreak.DurationSeconds);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task SaveAsync_RoundTrips_LongBreakSettings()
    {
        var path = TempPath();
        try
        {
            var settings = new AppSettings
            {
                MicroBreak = new BreakTierSettings { IntervalMinutes = 20, DurationSeconds = 30 },
                LongBreak = new BreakTierSettings { IntervalMinutes = 90, DurationSeconds = 360 },
            };
            await BuildWithTempPath(path).SaveAsync(settings);

            var json = await File.ReadAllTextAsync(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json)!;
            Assert.Equal(90, loaded.LongBreak.IntervalMinutes);
            Assert.Equal(360, loaded.LongBreak.DurationSeconds);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task SaveAsync_RoundTrips_AutoStartWithWindows()
    {
        var path = TempPath();
        try
        {
            var settings = new AppSettings { AutoStartWithWindows = true };
            await BuildWithTempPath(path).SaveAsync(settings);

            var json = await File.ReadAllTextAsync(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json)!;
            Assert.True(loaded.AutoStartWithWindows);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfMissing()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cm-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "appsettings.local.json");
        try
        {
            await BuildWithTempPath(path).SaveAsync(new AppSettings());
            Assert.True(File.Exists(path));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task SaveAsync_OverwritesPreviousFile()
    {
        var path = TempPath();
        try
        {
            var cm = BuildWithTempPath(path);
            await cm.SaveAsync(new AppSettings { MicroBreak = new BreakTierSettings { IntervalMinutes = 10, DurationSeconds = 30 } });
            await cm.SaveAsync(new AppSettings { MicroBreak = new BreakTierSettings { IntervalMinutes = 25, DurationSeconds = 30 } });

            var json = await File.ReadAllTextAsync(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json)!;
            Assert.Equal(25, loaded.MicroBreak.IntervalMinutes);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task SaveAsync_RoundTrips_SnoozeSettings()
    {
        var path = TempPath();
        try
        {
            var settings = new AppSettings { Snooze = new SnoozeSettings { IncrementMinutes = 7 } };
            await BuildWithTempPath(path).SaveAsync(settings);

            var json = await File.ReadAllTextAsync(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json)!;
            Assert.Equal(7, loaded.Snooze.IncrementMinutes);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task SaveAsync_RoundTrips_CalendarSettings()
    {
        var path = TempPath();
        try
        {
            var settings = new AppSettings
            {
                Calendar = new CalendarSettings
                {
                    Enabled = true,
                    TenantId = "tenant-123",
                    ClientId = "client-456",
                }
            };
            await BuildWithTempPath(path).SaveAsync(settings);

            var json = await File.ReadAllTextAsync(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json)!;
            Assert.True(loaded.Calendar.Enabled);
            Assert.Equal("tenant-123", loaded.Calendar.TenantId);
            Assert.Equal("client-456", loaded.Calendar.ClientId);
        }
        finally { TryDelete(path); }
    }

    // Atomic save writes through a sibling ".tmp" file then renames it onto the target.
    // A clean save must not leave that temp artifact behind.
    [Fact]
    public async Task SaveAsync_LeavesNoTempFileBehind()
    {
        var path = TempPath();
        try
        {
            await BuildWithTempPath(path).SaveAsync(new AppSettings());
            Assert.True(File.Exists(path), "config file should exist after save");
            Assert.False(File.Exists(path + ".tmp"), "temp file must be renamed away, not left behind");
        }
        finally { TryDelete(path); TryDelete(path + ".tmp"); }
    }

    // A stale temp file from a previously-interrupted save must not block the next save.
    [Fact]
    public async Task SaveAsync_OverwritesStaleTempFile()
    {
        var path = TempPath();
        try
        {
            await File.WriteAllTextAsync(path + ".tmp", "{ torn partial write");
            await BuildWithTempPath(path).SaveAsync(
                new AppSettings { MicroBreak = new BreakTierSettings { IntervalMinutes = 12, DurationSeconds = 30 } });

            var loaded = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(path))!;
            Assert.Equal(12, loaded.MicroBreak.IntervalMinutes);
            Assert.False(File.Exists(path + ".tmp"), "stale temp file should be consumed by the rename");
        }
        finally { TryDelete(path); TryDelete(path + ".tmp"); }
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"cm-{Guid.NewGuid():N}.json");

    private static void TryDelete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private sealed class OptionsMonitorStub(AppSettings value) : IOptionsMonitor<AppSettings>
    {
        public AppSettings CurrentValue => value;
        public AppSettings Get(string? name) => value;
        public IDisposable? OnChange(Action<AppSettings, string?> listener) => null;
    }
}
