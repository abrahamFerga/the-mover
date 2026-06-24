// TheMover.Calendar — contract consumed by CalendarSyncService (ARCH: ADR-0002)
namespace TheMover.Calendar;

public interface ICalendarClient
{
    /// <summary>Returns true if a calendar event with ShowAs=Busy/OOF is active right now.</summary>
    Task<bool> HasActiveMeetingAsync(CancellationToken ct = default);

    /// <summary>Acquires a token interactively (browser popup) and caches it.</summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>Clears the cached token and signs out.</summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>True if a valid cached token exists (may still expire or be revoked).</summary>
    Task<bool> IsConnectedAsync(CancellationToken ct = default);
}
