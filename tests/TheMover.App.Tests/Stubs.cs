// TheMover.App.Tests — shared IOptionsMonitor<AppSettings> stub used across test fixtures
using Microsoft.Extensions.Options;
using TheMover.App.Config;

namespace TheMover.App.Tests;

internal sealed class OptionsMonitorStub(AppSettings value) : IOptionsMonitor<AppSettings>
{
    public AppSettings CurrentValue => value;
    public AppSettings Get(string? name) => value;
    public IDisposable? OnChange(Action<AppSettings, string?> listener) => null;
}
