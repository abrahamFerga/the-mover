// TheMover.App — ARCH.md: Components / StartupRegistrar (ADR-0004: opt-in startup toggle)
using Microsoft.Win32;
using Microsoft.Extensions.Logging;

namespace TheMover.App.Shell;

public sealed class StartupRegistrar(ILogger<StartupRegistrar> logger)
{
    private const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TheMover";

    public void SetStartupEnabled(bool enabled, string executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: true);
            if (key is null)
            {
                logger.LogWarning("Could not open HKCU\\{Key} for writing", RegistryKey);
                return;
            }
            if (enabled)
            {
                key.SetValue(ValueName, $"\"{executablePath}\"");
                logger.LogInformation("Startup registration added: {Path}", executablePath);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                logger.LogInformation("Startup registration removed");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update startup registration");
        }
    }

}
