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

        // Atomic write: serialise to a sibling temp file, then replace the target in a
        // single rename. A crash or forced quit mid-write leaves the previous complete
        // file intact instead of a truncated one — Program.cs loads appsettings.local.json
        // as optional-but-not-fault-tolerant, so a torn file throws at startup binding and
        // bricks the app until the user deletes it by hand.
        var tempPath = ConfigPath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, json, ct);
            File.Move(tempPath, ConfigPath, overwrite: true);
        }
        catch
        {
            TryDeleteTemp(tempPath);
            throw;
        }
        logger.LogInformation("Settings saved to {Path}", ConfigPath);
    }

    private static void TryDeleteTemp(string path)
    {
        // Best-effort cleanup — the original config file is already safe either way.
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* leave the stray temp; the next save overwrites it */ }
    }
}
