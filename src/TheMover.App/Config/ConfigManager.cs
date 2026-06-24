// TheMover.App — ARCH.md: Components / ConfigManager
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TheMover.App.Config;

public sealed class ConfigManager(
    IOptionsMonitor<AppSettings> options,
    ILogger<ConfigManager> logger)
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheMover", "appsettings.local.json");

    public AppSettings Current => options.CurrentValue;

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(ConfigPath, json, ct);
        logger.LogInformation("Settings saved to {Path}", ConfigPath);
    }
}
