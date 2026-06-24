// TheMover.Calendar — Microsoft Graph calendarView integration (ADR-0002)
// Requires: an Azure AD App Registration with Calendars.Read scope and redirect URI http://localhost
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace TheMover.Calendar;

public sealed class GraphCalendarClient : ICalendarClient
{
    private static readonly string[] Scopes = ["Calendars.Read"];
    private const string CacheFileName = "the-mover.token.cache";

    private readonly HttpClient _http;
    private readonly string _cacheDir;
    // Optional factory: when set, ConnectAsync rebuilds the PCA from the latest
    // credentials so Settings changes take effect without an app restart.
    private readonly Func<(string clientId, string tenantId)>? _getCredentials;
    private IPublicClientApplication _pca;

    public GraphCalendarClient(string clientId, string tenantId, HttpClient httpClient)
        : this(clientId, tenantId, httpClient, getCredentials: null) { }

    // Extended constructor: pass a credential factory so DI callers can supply
    // live settings without importing Microsoft.Extensions.Options into this library.
    public GraphCalendarClient(
        string clientId, string tenantId, HttpClient httpClient,
        Func<(string clientId, string tenantId)>? getCredentials)
    {
        _http = httpClient;
        _getCredentials = getCredentials;
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TheMover");

        _pca = BuildPca(clientId, tenantId);
        AttachTokenCacheAsync(_pca).GetAwaiter().GetResult();
    }

    private IPublicClientApplication BuildPca(string clientId, string tenantId) =>
        PublicClientApplicationBuilder
            .Create(clientId)
            .WithTenantId(tenantId)
            .WithRedirectUri("http://localhost")
            .Build();

    private async Task AttachTokenCacheAsync(IPublicClientApplication pca)
    {
        Directory.CreateDirectory(_cacheDir);
        // On Windows, MsalCacheHelper applies DPAPI encryption by default — satisfies SPEC
        // requirement that the refresh token must not be stored in a plaintext file.
        var storageProps = new StorageCreationPropertiesBuilder(CacheFileName, _cacheDir)
            .Build();
        var helper = await MsalCacheHelper.CreateAsync(storageProps);
        helper.RegisterCache(pca.UserTokenCache);
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        var accounts = await _pca.GetAccountsAsync();
        return accounts.Any();
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        // Rebuild the PCA from the latest credentials so that credentials saved
        // in Settings are honoured immediately without an app restart.
        if (_getCredentials is not null)
        {
            var (clientId, tenantId) = _getCredentials();
            _pca = BuildPca(clientId, tenantId);
            await AttachTokenCacheAsync(_pca);
        }

        await _pca.AcquireTokenInteractive(Scopes)
            .WithUseEmbeddedWebView(false)
            .ExecuteAsync(ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        foreach (var account in await _pca.GetAccountsAsync())
            await _pca.RemoveAsync(account);
    }

    public async Task<bool> HasActiveMeetingAsync(CancellationToken ct = default)
    {
        string? token;
        try
        {
            var accounts = await _pca.GetAccountsAsync();
            var account = accounts.FirstOrDefault();
            if (account is null) return false;

            var result = await _pca.AcquireTokenSilent(Scopes, account).ExecuteAsync(ct);
            token = result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            return false;  // not signed in — graceful degradation
        }
        catch
        {
            return false;  // network or auth error — graceful degradation (breaks fire normally)
        }

        return await QueryGraphAsync(token, ct);
    }

    // Extracted so tests can drive the HTTP-response parsing without MSAL.
    internal async Task<bool> QueryGraphAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var url = $"https://graph.microsoft.com/v1.0/me/calendarView" +
                      $"?startDateTime={now:O}&endDateTime={now.AddSeconds(1):O}" +
                      $"&$select=subject,showAs&$filter=showAs eq 'busy' or showAs eq 'oof'";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var value = doc.RootElement.GetProperty("value");
            return value.GetArrayLength() > 0;
        }
        catch
        {
            return false;  // network error — graceful degradation
        }
    }
}
