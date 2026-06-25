using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TheMover.App.Logging;

namespace TheMover.App.Tests.Logging;

public sealed class EventLoggerTests
{
    private static EventLogger Build(string path) =>
        new(path, NullLogger<EventLogger>.Instance);

    [Fact]
    public void Log_WritesJsonlLineWithEventName()
    {
        var path = TempPath();
        try
        {
            Build(path).Log(AppEventType.Heartbeat);
            var line = File.ReadAllText(path).TrimEnd();
            Assert.Contains("\"event\":\"Heartbeat\"", line);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Log_IncludesTimestamp()
    {
        var path = TempPath();
        try
        {
            var before = DateTimeOffset.UtcNow;
            Build(path).Log(AppEventType.Heartbeat);
            var line = File.ReadAllText(path);
            Assert.Contains("\"ts\":", line);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Log_AppendsMultipleLines()
    {
        var path = TempPath();
        try
        {
            var logger = Build(path);
            logger.Log(AppEventType.Heartbeat);
            logger.Log(AppEventType.BreakFired);
            var lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Log_IncludesExtraFields()
    {
        var path = TempPath();
        try
        {
            Build(path).Log(AppEventType.BreakFired, new Dictionary<string, object?> { ["tier"] = "Long" });
            var line = File.ReadAllText(path);
            Assert.Contains("\"tier\":\"Long\"", line);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Log_CreatesDirectoryIfMissing()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ev-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "events.jsonl");
        try
        {
            Build(path).Log(AppEventType.Heartbeat);
            Assert.True(File.Exists(path));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Log_WritesValidJson()
    {
        var path = TempPath();
        try
        {
            Build(path).Log(AppEventType.OverlayShown, new Dictionary<string, object?> { ["tier"] = "Micro", ["exerciseId"] = "neck-rolls" });
            var line = File.ReadAllText(path).TrimEnd();
            var doc = JsonDocument.Parse(line);
            Assert.Equal("OverlayShown", doc.RootElement.GetProperty("event").GetString());
            Assert.Equal("Micro", doc.RootElement.GetProperty("tier").GetString());
            Assert.Equal("neck-rolls", doc.RootElement.GetProperty("exerciseId").GetString());
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Log_SwallowsExceptionForInvalidPath()
    {
        var logger = Build(@"Z:\cannot-exist-xyzzy\file.jsonl");
        var ex = Record.Exception(() => logger.Log(AppEventType.Heartbeat));
        Assert.Null(ex);
    }

    // Passing null extra must not throw and must produce a valid record with only ts+event.
    [Fact]
    public void Log_WithNullExtra_WritesOnlyTimestampAndEvent()
    {
        var path = TempPath();
        try
        {
            Build(path).Log(AppEventType.Heartbeat, null);
            var doc = JsonDocument.Parse(File.ReadAllText(path).TrimEnd());
            Assert.True(doc.RootElement.TryGetProperty("ts", out _));
            Assert.True(doc.RootElement.TryGetProperty("event", out _));
            Assert.Equal(2, doc.RootElement.EnumerateObject().Count());
        }
        finally { TryDelete(path); }
    }

    // Extra dictionary must merge all keys into the top-level JSON object.
    [Fact]
    public void Log_WithExtraDictionary_MergesAllKeys()
    {
        var path = TempPath();
        try
        {
            Build(path).Log(AppEventType.BreakFired,
                new Dictionary<string, object?> { ["tier"] = "Micro", ["source"] = "overlay" });
            var line = File.ReadAllText(path);
            Assert.Contains("\"tier\":\"Micro\"", line);
            Assert.Contains("\"source\":\"overlay\"", line);
        }
        finally { TryDelete(path); }
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"ev-{Guid.NewGuid():N}.jsonl");

    private static void TryDelete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
