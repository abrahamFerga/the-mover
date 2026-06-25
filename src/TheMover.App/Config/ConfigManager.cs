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
    private static readonly JsonSerializerOptions SaveOpts = new() { WriteIndented = true };

    private static readonly string DefaultConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheMover", "appsettings.local.json");

    // Override in tests via object initializer — avoids writing to %LocalAppData% in CI.
    internal string ConfigPath { get; init; } = DefaultConfigPath;

    public AppSettings Current => options.CurrentValue;

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var json = JsonSerializer.Serialize(settings, SaveOpts);
        await File.WriteAllTextAsync(ConfigPath, json, ct);
        logger.LogInformation("Settings saved to {Path}", ConfigPath);
    }
}
