using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using TheMover.App.Logging;

namespace TheMover.App.Tests.Logging;

public sealed class HeartbeatServiceTests
{
    private static (HeartbeatService service, EventLogger eventLogger, string path) Build()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hb-{Guid.NewGuid():N}.jsonl");
        var eventLogger = new EventLogger(path, NullLogger<EventLogger>.Instance);
        var service = new HeartbeatService(eventLogger, NullLogger<HeartbeatService>.Instance);
        return (service, eventLogger, path);
    }

    [Fact]
    public void WriteHeartbeat_WritesHeartbeatEvent()
    {
        var (service, _, path) = Build();
        try
        {
            service.WriteHeartbeat();
            Assert.True(File.Exists(path));
            Assert.Contains("\"event\":\"Heartbeat\"", File.ReadAllText(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void WriteHeartbeat_CalledTwice_AppendsTwoLines()
    {
        var (service, _, path) = Build();
        try
        {
            service.WriteHeartbeat();
            service.WriteHeartbeat();
            Assert.Equal(2, File.ReadAllLines(path).Length);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task OnStart_WritesHeartbeatBeforeFirstTimerTick()
    {
        var (service, _, path) = Build();
        try
        {
            using var cts = new CancellationTokenSource();
            await service.StartAsync(cts.Token);

            // Brief wait so the background task can execute WriteHeartbeat() before the 24h timer.
            await Task.Delay(150);

            Assert.True(File.Exists(path));
            Assert.Contains("\"event\":\"Heartbeat\"", File.ReadAllText(path));

            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
