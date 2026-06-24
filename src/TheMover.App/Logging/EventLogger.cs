// TheMover.App — ARCH.md: Components / EventLogger (append-only JSONL for SPEC success metrics)
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TheMover.App.Logging;

public sealed class EventLogger
{
    private static readonly JsonSerializerOptions Opts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _logPath;
    private readonly ILogger<EventLogger> _logger;

    private static string DefaultLogPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheMover", "events.jsonl");

    public EventLogger(ILogger<EventLogger> logger) : this(DefaultLogPath(), logger) { }

    internal EventLogger(string logPath, ILogger<EventLogger> logger)
    {
        _logPath = logPath;
        _logger = logger;
    }

    public void Log(AppEventType eventType, object? extra = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            var record = new Dictionary<string, object?>
            {
                ["ts"] = DateTimeOffset.UtcNow.ToString("O"),
                ["event"] = eventType.ToString()
            };
            if (extra is IDictionary<string, object?> d)
            {
                foreach (var (k, v) in d) record[k] = v;
            }
            File.AppendAllText(_logPath, JsonSerializer.Serialize(record, Opts) + "\n");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append event log entry for {Event}", eventType);
        }
    }
}
