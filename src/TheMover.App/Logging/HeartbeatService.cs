// TheMover.App — writes a daily Heartbeat event to the local event log (SPEC: 30-day retention metric)
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TheMover.App.Logging;

public sealed class HeartbeatService(EventLogger eventLogger, ILogger<HeartbeatService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Write once immediately so every launch is captured even if the app runs < 24 h.
        WriteHeartbeat();

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            WriteHeartbeat();
        }
    }

    private void WriteHeartbeat()
    {
        eventLogger.Log(AppEventType.Heartbeat);
        logger.LogDebug("Daily heartbeat written");
    }
}
