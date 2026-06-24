// TheMover.Calendar.Tests — GraphCalendarClient.QueryGraphAsync HTTP-response parsing
using System.Net;
using System.Net.Http;
using TheMover.Calendar;

namespace TheMover.Calendar.Tests;

public sealed class GraphCalendarClientTests
{
    private static GraphCalendarClient BuildWithHandler(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler);
        // Use public constructor with a dummy clientId/tenantId so MSAL is initialised
        // but no real network call is ever made (token cache is empty, tests call QueryGraphAsync
        // directly so the MSAL path is bypassed entirely).
        return new GraphCalendarClient("00000000-0000-0000-0000-000000000000", "common", http);
    }

    [Fact]
    public async Task WhenGraphReturnsNonEmptyValue_ReturnsMeetingActive()
    {
        var body = """{"value":[{"subject":"Standup","showAs":"busy"}]}""";
        var client = BuildWithHandler(new FixedResponseHandler(HttpStatusCode.OK, body));

        var result = await client.QueryGraphAsync("fake-token");

        Assert.True(result);
    }

    [Fact]
    public async Task WhenGraphReturnsEmptyValue_ReturnsFalse()
    {
        var body = """{"value":[]}""";
        var client = BuildWithHandler(new FixedResponseHandler(HttpStatusCode.OK, body));

        var result = await client.QueryGraphAsync("fake-token");

        Assert.False(result);
    }

    [Fact]
    public async Task WhenGraphReturnsNonSuccess_ReturnsFalse()
    {
        var client = BuildWithHandler(new FixedResponseHandler(HttpStatusCode.Unauthorized, "{}"));

        var result = await client.QueryGraphAsync("fake-token");

        Assert.False(result);
    }

    [Fact]
    public async Task WhenGraphThrows_ReturnsFalse()
    {
        var client = BuildWithHandler(new ThrowingHandler());

        var result = await client.QueryGraphAsync("fake-token");

        Assert.False(result);
    }

    [Fact]
    public async Task WhenGraphReturnsMalformedJson_ReturnsFalse()
    {
        var client = BuildWithHandler(new FixedResponseHandler(HttpStatusCode.OK, "not-json"));

        var result = await client.QueryGraphAsync("fake-token");

        Assert.False(result);
    }

    // Sends a Bearer token in the Authorization header
    [Fact]
    public async Task QueryGraph_SetsAuthorizationHeader()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"value":[]}""");
        var client = BuildWithHandler(handler);

        await client.QueryGraphAsync("my-access-token");

        Assert.Equal("Bearer my-access-token",
            handler.LastRequest?.Headers.Authorization?.ToString());
    }

    // The 4-arg constructor's factory must be lazy — called inside ConnectAsync,
    // not at DI startup (when credentials may not yet be saved by the user).
    [Fact]
    public async Task WithGetCredentialsFactory_FactoryNotCalledAtConstruction()
    {
        var callCount = 0;
        var client = new GraphCalendarClient(
            "00000000-0000-0000-0000-000000000000", "common",
            new HttpClient(),
            getCredentials: () => { callCount++; return ("00000000-0000-0000-0000-000000000000", "common"); });

        Assert.Equal(0, callCount);  // factory must not fire in the constructor
        Assert.False(await client.IsConnectedAsync()); // fresh PCA → no accounts
    }

    private sealed class FixedResponseHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("simulated network error");
    }

    private sealed class CapturingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            });
        }
    }
}
