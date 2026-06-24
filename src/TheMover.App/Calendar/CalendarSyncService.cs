// TheMover.App — polls ICalendarClient every 60 s, sets BreakTimerState.HeldForMeeting
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheMover.App.Config;
using TheMover.Calendar;
using TheMover.Scheduler;

namespace TheMover.App.Calendar;

public sealed class CalendarSyncService : BackgroundService
{
    private readonly ICalendarClient _calendar;
    private readonly BreakTimerState _state;
    private readonly IOptionsMonitor<AppSettings> _options;
    private readonly ILogger<CalendarSyncService> _logger;
    private readonly Action<bool>? _updateMeetingTooltip;

    public CalendarSyncService(
        ICalendarClient calendar,
        BreakTimerState state,
        IOptionsMonitor<AppSettings> options,
        ILogger<CalendarSyncService> logger,
        Shell.TrayIconService tray)
        : this(calendar, state, options, logger, tray.UpdateMeetingTooltip) { }

    internal CalendarSyncService(
        ICalendarClient calendar,
        BreakTimerState state,
        IOptionsMonitor<AppSettings> options,
        ILogger<CalendarSyncService> logger,
        Action<bool>? updateMeetingTooltip = null)
    {
        _calendar = calendar;
        _state = state;
        _options = options;
        _logger = logger;
        _updateMeetingTooltip = updateMeetingTooltip;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (!stoppingToken.IsCancellationRequested)
        {
            await PollAsync(stoppingToken);
            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    internal async Task PollAsync(CancellationToken ct = default)
    {
        if (!_options.CurrentValue.Calendar.Enabled)
        {
            if (_state.HeldForMeeting)
            {
                _state.HeldForMeeting = false;
                _updateMeetingTooltip?.Invoke(false);
            }
            return;
        }

        try
        {
            var inMeeting = await _calendar.HasActiveMeetingAsync(ct);
            if (_state.HeldForMeeting != inMeeting)
            {
                _state.HeldForMeeting = inMeeting;
                _updateMeetingTooltip?.Invoke(inMeeting);
                _logger.LogInformation("Meeting state changed: HeldForMeeting={InMeeting}", inMeeting);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Graceful degradation: if Graph is unreachable, don't suppress breaks
            _state.HeldForMeeting = false;
            _logger.LogWarning(ex, "Calendar poll failed — breaks will fire normally");
        }
    }
}
