// TheMover.App.Tests — CalendarSyncService poll logic
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheMover.App.Calendar;
using TheMover.App.Config;
using TheMover.App.Tests;
using TheMover.Calendar;
using TheMover.Scheduler;

namespace TheMover.App.Tests.Calendar;

public sealed class CalendarSyncServiceTests
{
    private static (CalendarSyncService Service, BreakTimerState State) Build(
        AppSettings settings,
        ICalendarClient client)
    {
        var state = new BreakTimerState();
        var svc = new CalendarSyncService(
            client,
            state,
            new OptionsMonitorStub(settings),
            NullLogger<CalendarSyncService>.Instance);
        return (svc, state);
    }

    [Fact]
    public async Task WhenDisabled_SetsHeldForMeetingFalse()
    {
        var settings = new AppSettings { Calendar = new CalendarSettings { Enabled = false } };
        var client = new FakeCalendarClient(isConnected: true, inMeeting: true);
        var (svc, state) = Build(settings, client);
        state.HeldForMeeting = true;

        await svc.PollAsync();

        Assert.False(state.HeldForMeeting);
        Assert.Equal(0, client.HasActiveMeetingCallCount);
    }

    [Fact]
    public async Task WhenInMeeting_SetsHeldForMeetingTrue()
    {
        var settings = new AppSettings { Calendar = new CalendarSettings { Enabled = true } };
        var client = new FakeCalendarClient(isConnected: true, inMeeting: true);
        var (svc, state) = Build(settings, client);

        await svc.PollAsync();

        Assert.True(state.HeldForMeeting);
    }

    [Fact]
    public async Task WhenNotInMeeting_SetsHeldForMeetingFalse()
    {
        var settings = new AppSettings { Calendar = new CalendarSettings { Enabled = true } };
        var client = new FakeCalendarClient(isConnected: true, inMeeting: false);
        var (svc, state) = Build(settings, client);
        state.HeldForMeeting = true;

        await svc.PollAsync();

        Assert.False(state.HeldForMeeting);
    }

    [Fact]
    public async Task WhenClientThrows_SetsHeldForMeetingFalse()
    {
        var settings = new AppSettings { Calendar = new CalendarSettings { Enabled = true } };
        var client = new ThrowingCalendarClient();
        var (svc, state) = Build(settings, client);
        state.HeldForMeeting = true;

        await svc.PollAsync();

        Assert.False(state.HeldForMeeting);
    }

    [Fact]
    public async Task WhenClientThrows_AndWasInMeeting_InvokesTooltipCallbackFalse()
    {
        var settings = new AppSettings { Calendar = new CalendarSettings { Enabled = true } };
        var client = new ThrowingCalendarClient();
        var state = new BreakTimerState { HeldForMeeting = true };
        var updates = new List<bool>();
        var svc = new CalendarSyncService(client, state, new OptionsMonitorStub(settings),
            NullLogger<CalendarSyncService>.Instance, updateMeetingTooltip: updates.Add);

        await svc.PollAsync();

        Assert.Single(updates);
        Assert.False(updates[0]);
    }

    [Fact]
    public async Task WhenMeetingStateChanges_InvokesTooltipCallback()
    {
        var settings = new AppSettings { Calendar = new CalendarSettings { Enabled = true } };
        var client = new FakeCalendarClient(isConnected: true, inMeeting: true);
        var state = new BreakTimerState();
        var updates = new List<bool>();
        var svc = new CalendarSyncService(client, state, new OptionsMonitorStub(settings),
            NullLogger<CalendarSyncService>.Instance, updateMeetingTooltip: updates.Add);

        await svc.PollAsync();

        Assert.Single(updates);
        Assert.True(updates[0]);
    }

    [Fact]
    public async Task WhenMeetingStateUnchanged_DoesNotInvokeTooltipCallback()
    {
        var settings = new AppSettings { Calendar = new CalendarSettings { Enabled = true } };
        var client = new FakeCalendarClient(isConnected: true, inMeeting: false);
        var state = new BreakTimerState { HeldForMeeting = false };
        var updates = new List<bool>();
        var svc = new CalendarSyncService(client, state, new OptionsMonitorStub(settings),
            NullLogger<CalendarSyncService>.Instance, updateMeetingTooltip: updates.Add);

        await svc.PollAsync();

        Assert.Empty(updates);
    }

    [Fact]
    public async Task WhenDisabledAndWasInMeeting_InvokesTooltipCallbackFalse()
    {
        var settings = new AppSettings { Calendar = new CalendarSettings { Enabled = false } };
        var client = new FakeCalendarClient(isConnected: true, inMeeting: true);
        var state = new BreakTimerState { HeldForMeeting = true };
        var updates = new List<bool>();
        var svc = new CalendarSyncService(client, state, new OptionsMonitorStub(settings),
            NullLogger<CalendarSyncService>.Instance, updateMeetingTooltip: updates.Add);

        await svc.PollAsync();

        Assert.Single(updates);
        Assert.False(updates[0]);
    }

    // -----------------------------------------------------------------------

    private sealed class FakeCalendarClient(bool isConnected, bool inMeeting) : ICalendarClient
    {
        public int HasActiveMeetingCallCount { get; private set; }
        public Task<bool> HasActiveMeetingAsync(CancellationToken ct = default)
        {
            HasActiveMeetingCallCount++;
            return Task.FromResult(inMeeting);
        }
        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsConnectedAsync(CancellationToken ct = default) => Task.FromResult(isConnected);
    }

    private sealed class ThrowingCalendarClient : ICalendarClient
    {
        public Task<bool> HasActiveMeetingAsync(CancellationToken ct = default) =>
            throw new HttpRequestException("Network error");
        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsConnectedAsync(CancellationToken ct = default) => Task.FromResult(false);
    }
}
