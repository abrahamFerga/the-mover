// TheMover.App — polls GetLastInputInfo every 5 s; sets BreakTimerState.IdleDetectedAt
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TheMover.App.Shell;
using TheMover.Scheduler;

namespace TheMover.App.Idle;

public sealed class IdleMonitorService : BackgroundService
{
    private const int IdleThresholdSeconds = 120;
    private const int PollIntervalSeconds = 5;

    private readonly BreakTimerState _state;
    private readonly Func<TimeSpan> _getIdleTime;
    private readonly Action<bool> _updateTooltip;
    private readonly ILogger<IdleMonitorService> _logger;
    private readonly Func<DateTimeOffset> _clock;

    public IdleMonitorService(
        BreakTimerState state,
        TrayIconService tray,
        ILogger<IdleMonitorService> logger)
        : this(state, GetSystemIdleTime, tray.UpdateTooltip, logger) { }

    // clock defaults to DateTimeOffset.UtcNow; tests inject a fixed value for exact assertions.
    internal IdleMonitorService(
        BreakTimerState state,
        Func<TimeSpan> getIdleTime,
        Action<bool> updateTooltip,
        ILogger<IdleMonitorService> logger,
        Func<DateTimeOffset>? clock = null)
    {
        _state = state;
        _getIdleTime = getIdleTime;
        _updateTooltip = updateTooltip;
        _logger = logger;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(PollIntervalSeconds));
            while (!stoppingToken.IsCancellationRequested)
            {
                CheckIdle();
                try { await timer.WaitForNextTickAsync(stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _state.IdleDetectedAt = null;
        }
    }

    // On suspend: treat as idle so break intervals reset on resume.
    // On resume: clear idle so breaks don't fire the moment the lid opens.
    internal void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        var now = _clock();
        if (e.Mode == PowerModes.Suspend)
        {
            _state.IdleDetectedAt = now;
            _updateTooltip(true);
            _logger.LogInformation("System suspending — treating as idle");
        }
        else if (e.Mode == PowerModes.Resume)
        {
            _state.IdleDetectedAt = null;
            _state.LastMicroBreakAt = now;
            _state.LastLongBreakAt = now;
            _updateTooltip(false);
            _logger.LogInformation("System resumed — break intervals reset");
        }
    }

    internal void CheckIdle()
    {
        var idle = _getIdleTime();
        var now = _clock();

        if (idle >= TimeSpan.FromSeconds(IdleThresholdSeconds))
        {
            if (!_state.IdleDetectedAt.HasValue)
            {
                _state.IdleDetectedAt = now;
                _updateTooltip(true);
                _logger.LogInformation("Idle detected after {Seconds}s", (int)idle.TotalSeconds);
            }
        }
        else
        {
            if (_state.IdleDetectedAt.HasValue)
            {
                _state.IdleDetectedAt = null;
                // Reset break intervals so the user isn't nagged immediately on return
                _state.LastMicroBreakAt = now;
                _state.LastLongBreakAt = now;
                _updateTooltip(false);
                _logger.LogInformation("Activity resumed after idle; break intervals reset");
            }
        }
    }

    private static TimeSpan GetSystemIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        uint idleMs = unchecked(GetTickCount() - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMs);
    }

    [DllImport("user32.dll", SetLastError = false)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("kernel32.dll")]
    private static extern uint GetTickCount();

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }
}
