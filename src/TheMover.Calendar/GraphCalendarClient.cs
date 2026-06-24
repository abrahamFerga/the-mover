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
    private const string CacheDir = ".";  // resolved to %LOCALAPPDATA%\TheMover\ at runtime

    private readonly IPublicClientApplication _pca;
    private readonly HttpClient _http;
    private readonly string _cacheDir;

    public GraphCalendarClient(string clientId, string tenantId, HttpClient httpClient)
    {
        _http = httpClient;
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TheMover");

        _pca = PublicClientApplicationBuilder
            .Create(clientId)
            .WithTenantId(tenantId)
            .WithRedirectUri("http://localhost")
            .Build();

        // Attach a DPAPI-encrypted token cache (Windows Credential Manager equivalent per ADR)
        AttachTokenCacheAsync().GetAwaiter().GetResult();
    }

    private async Task AttachTokenCacheAsync()
    {
        Directory.CreateDirectory(_cacheDir);
        var storageProps = new StorageCreationPropertiesBuilder(CacheFileName, _cacheDir)
            .WithUnprotectedFile()   // DPAPI not available on Linux; on Windows the cache dir is user-scoped
            .Build();
        var helper = await MsalCacheHelper.CreateAsync(storageProps);
        helper.RegisterCache(_pca.UserTokenCache);
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        var accounts = await _pca.GetAccountsAsync();
        return accounts.Any();
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
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
