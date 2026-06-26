// TheMover.App — writes a daily Heartbeat event to the local event log (SPEC: 30-day retention metric)
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TheMover.App.Logging;

public sealed class HeartbeatService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly EventLogger _eventLogger;
    private readonly ILogger<HeartbeatService> _logger;
    private readonly Action? _onWritten;

    public HeartbeatService(EventLogger eventLogger, ILogger<HeartbeatService> logger)
        : this(eventLogger, logger, null) { }

    // onWritten fires after each WriteHeartbeat() call — used by tests for deterministic
    // synchronization instead of Task.Delay (ExecuteAsync runs on a thread-pool thread).
    internal HeartbeatService(EventLogger eventLogger, ILogger<HeartbeatService> logger, Action? onWritten)
    {
        _eventLogger = eventLogger;
        _logger = logger;
        _onWritten = onWritten;
    }

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

    internal void WriteHeartbeat()
    {
        _eventLogger.Log(AppEventType.Heartbeat);
        _onWritten?.Invoke();
        _logger.LogDebug("Daily heartbeat written");
    }
}
