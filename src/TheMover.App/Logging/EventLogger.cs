// TheMover.App — ARCH.md: Components / EventLogger (append-only JSONL for SPEC success metrics)
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TheMover.App.Logging;

public sealed class EventLogger
{
    private static readonly JsonSerializerOptions Opts = new();

    private readonly string _logPath;
    private readonly ILogger<EventLogger> _logger;
    // EventLogger is a singleton written to from several background-service threads
    // (overlay, command handler, heartbeat). Serialise the file appends so two
    // concurrent calls can't collide on the file handle and silently drop an event.
    private readonly object _writeLock = new();

    private static string DefaultLogPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheMover", "events.jsonl");

    public EventLogger(ILogger<EventLogger> logger) : this(DefaultLogPath(), logger) { }

    internal EventLogger(string logPath, ILogger<EventLogger> logger)
    {
        _logPath = logPath;
        _logger = logger;
        // Create the log directory once at construction rather than on every Log() call.
        // Directory.CreateDirectory is idempotent but still a syscall; hoisting it here
        // avoids the overhead on the write hot-path.
        try { Directory.CreateDirectory(Path.GetDirectoryName(logPath)!); }
        catch (Exception ex) { logger.LogWarning(ex, "Could not create event log directory — events will not be persisted"); }
    }

    public void Log(AppEventType eventType, IReadOnlyDictionary<string, object?>? extra = null)
    {
        try
        {
            var record = new Dictionary<string, object?>
            {
                ["ts"] = DateTimeOffset.UtcNow.ToString("O"),
                ["event"] = eventType.ToString()
            };
            if (extra is not null)
            {
                foreach (var (k, v) in extra) record[k] = v;
            }
            var line = JsonSerializer.Serialize(record, Opts) + "\n";
            lock (_writeLock)
            {
                File.AppendAllText(_logPath, line);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append event log entry for {Event}", eventType);
        }
    }
}
