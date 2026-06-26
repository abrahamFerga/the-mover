// TheMover.App.Tests — StartupRegistrar: registry write/delete + empty-path guard
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using TheMover.App.Shell;

namespace TheMover.App.Tests.Shell;

public sealed class StartupRegistrarTests : IDisposable
{
    // Isolated test key — never touches the real HKCU\...\Run entry.
    private const string TestKey = @"Software\TheMover.Tests\StartupRegistrar";
    private const string TestValue = "TheMoverTest";

    public void Dispose() =>
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\TheMover.Tests", throwOnMissingSubKey: false);

    private StartupRegistrar Build() =>
        new(NullLogger<StartupRegistrar>.Instance, TestKey, TestValue);

    [Fact]
    public void Enable_WithEmptyPath_DoesNotWriteRegistryValue()
    {
        Build().SetStartupEnabled(true, "");

        using var key = Registry.CurrentUser.OpenSubKey(TestKey);
        Assert.Null(key?.GetValue(TestValue));
    }

    [Fact]
    public void Enable_WritesQuotedExecutablePath()
    {
        Registry.CurrentUser.CreateSubKey(TestKey).Dispose(); // ensure key exists before write

        Build().SetStartupEnabled(true, @"C:\Program Files\TheMover\TheMover.App.exe");

        using var key = Registry.CurrentUser.OpenSubKey(TestKey);
        Assert.Equal(
            @"""C:\Program Files\TheMover\TheMover.App.exe""",
            key?.GetValue(TestValue) as string);
    }

    [Fact]
    public void Disable_RemovesExistingValue()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(TestKey, writable: true))
            key.SetValue(TestValue, @"""C:\TheMover.App.exe""");

        Build().SetStartupEnabled(false, "");

        using var readKey = Registry.CurrentUser.OpenSubKey(TestKey);
        Assert.Null(readKey?.GetValue(TestValue));
    }

    [Fact]
    public void Disable_WhenValueAbsent_DoesNotThrow()
    {
        var ex = Record.Exception(() => Build().SetStartupEnabled(false, ""));
        Assert.Null(ex);
    }
}
