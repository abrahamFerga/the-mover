// TheMover.App — ARCH.md: Components / StartupRegistrar (ADR-0004: opt-in startup toggle)
using Microsoft.Win32;
using Microsoft.Extensions.Logging;

namespace TheMover.App.Shell;

public sealed class StartupRegistrar
{
    private readonly ILogger<StartupRegistrar> _logger;
    private readonly string _registryKeyPath;
    private readonly string _valueName;

    public StartupRegistrar(ILogger<StartupRegistrar> logger)
        : this(logger, @"Software\Microsoft\Windows\CurrentVersion\Run", "TheMover") { }

    internal StartupRegistrar(ILogger<StartupRegistrar> logger, string registryKeyPath, string valueName)
    {
        _logger = logger;
        _registryKeyPath = registryKeyPath;
        _valueName = valueName;
    }

    public void SetStartupEnabled(bool enabled, string executablePath)
    {
        if (enabled && string.IsNullOrEmpty(executablePath))
        {
            _logger.LogWarning("Startup registration skipped: executable path is unknown");
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_registryKeyPath, writable: true);
            if (key is null)
            {
                _logger.LogWarning("Could not open HKCU\\{Key} for writing", _registryKeyPath);
                return;
            }
            if (enabled)
            {
                key.SetValue(_valueName, $"\"{executablePath}\"");
                _logger.LogInformation("Startup registration added: {Path}", executablePath);
            }
            else
            {
                key.DeleteValue(_valueName, throwOnMissingValue: false);
                _logger.LogInformation("Startup registration removed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update startup registration");
        }
    }
}
