using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Remote;

/// <summary>
/// Talks to a remote's Management API as its API user. Every request, the token request included, passes the allowlist
/// and the remote's own host check before it is sent.
/// </summary>
public sealed class RemoteClient
{
    public const string HttpClientName = "Transfr.Remote";

    // Renew a little before the remote expires the token, so a request never leaves with a token about to lapse.
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAt)> tokens = new(StringComparer.Ordinal);

    public RemoteClient(IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
    {
        this.httpClientFactory = httpClientFactory;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Sends a request to the remote. <paramref name="pathAndQuery"/> is relative to the site root, e.g.
    /// <c>/umbraco/management/api/v1/tree/document/root</c>.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(
        RemoteDefinition remote,
        HttpMethod method,
        string pathAndQuery,
        bool authenticate = true,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(remote);

        using var request = CreateRequest(remote, method, pathAndQuery, content);
        if (authenticate)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await this.GetTokenAsync(remote, cancellationToken));
        }

        return await this.httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// GETs a path and returns the body. Throws <see cref="HttpRequestException"/>, with the status code, for anything
    /// but a success.
    /// </summary>
    public async Task<string> GetStringAsync(RemoteDefinition remote, string pathAndQuery, CancellationToken cancellationToken)
    {
        using var response = await this.SendAsync(remote, HttpMethod.Get, pathAndQuery, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"The remote answered {(int)response.StatusCode} for {pathAndQuery.Split('?')[0]}.", null, response.StatusCode);
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(RemoteDefinition remote, HttpMethod method, string pathAndQuery, HttpContent? content)
    {
        var baseUrl = new Uri(remote.BaseUrl);
        var requestUri = new Uri(baseUrl, pathAndQuery);
        RemoteRequestPolicy.EnsureAllowed(method.Method, requestUri, baseUrl);
        return new HttpRequestMessage(method, requestUri) { Content = content };
    }

    /// <summary>
    /// Signs in with the remote's client credentials now, ignoring any cached token: proves the credentials work.
    /// Throws <see cref="RemoteAuthenticationException"/> when the remote refuses them.
    /// </summary>
    public Task AuthenticateAsync(RemoteDefinition remote, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(remote);
        return this.GetTokenAsync(remote, cancellationToken, useCache: false);
    }

    // Tokens are cached per set of credentials, not per alias: two users may each have a runtime remote with the same
    // alias but different credentials.
    private static string CacheKey(RemoteDefinition remote)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{remote.BaseUrl}\n{remote.ClientId}\n{remote.ClientSecret}")));

    private async Task<string> GetTokenAsync(RemoteDefinition remote, CancellationToken cancellationToken, bool useCache = true)
    {
        var now = this.timeProvider.GetUtcNow();
        var cacheKey = CacheKey(remote);
        if (useCache && this.tokens.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt - ExpiryMargin > now)
        {
            return cached.Token;
        }

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = remote.ClientId,
            ["client_secret"] = remote.ClientSecret,
        });
        using var request = CreateRequest(remote, HttpMethod.Post, RemoteRequestPolicy.ApiBase + "/security/back-office/token", form);
        using var response = await this.httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new RemoteAuthenticationException($"The remote refused the client credentials ({(int)response.StatusCode}).");
        }

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var token = json.RootElement.GetProperty("access_token").GetString()
            ?? throw new RemoteAuthenticationException("The remote returned no access token.");
        var expiresIn = json.RootElement.TryGetProperty("expires_in", out var seconds) ? seconds.GetInt32() : 0;

        this.tokens[cacheKey] = (token, now.AddSeconds(expiresIn));
        return token;
    }
}