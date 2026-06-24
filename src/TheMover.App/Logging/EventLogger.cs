// TheMover.App — ARCH.md: Components / EventLogger (append-only JSONL for SPEC success metrics)
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TheMover.App.Logging;

public sealed class EventLogger(ILogger<EventLogger> logger)
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheMover", "events.jsonl");

    private static readonly JsonSerializerOptions Opts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public void Log(AppEventType eventType, object? extra = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var record = new Dictionary<string, object?>
            {
                ["ts"] = DateTimeOffset.UtcNow.ToString("O"),
                ["event"] = eventType.ToString()
            };
            if (extra is IDictionary<string, object?> d)
            {
                foreach (var (k, v) in d) record[k] = v;
            }
            File.AppendAllText(LogPath, JsonSerializer.Serialize(record, Opts) + "\n");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to append event log entry for {Event}", eventType);
        }
    }
}
