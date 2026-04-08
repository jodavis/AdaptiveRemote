using System.Net.Http;
using System.Text.Json;
using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Backend;

/// <summary>
/// Acquires and caches OAuth2 access tokens from AWS Cognito using the
/// Client Credentials flow. Token refresh is lazy: the cached token is
/// returned until it is within <see cref="ExpiryBuffer"/> of expiring,
/// at which point a new token is acquired.
/// </summary>
internal sealed class CognitoTokenService : ICognitoTokenService, IDisposable
{
    // Refresh the token this many seconds before it actually expires.
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromSeconds(60);

    private readonly BackendSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly MessageLogger _log;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private string? _tokenEndpoint;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public CognitoTokenService(
        IOptions<BackendSettings> settings,
        ILogger<CognitoTokenService> logger)
    {
        _settings = settings.Value;
        _httpClient = new HttpClient();
        _log = new MessageLogger(logger);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry - ExpiryBuffer)
            {
                return _cachedToken;
            }

            _log.CognitoTokenService_AcquiringToken();
            try
            {
                string endpoint = await GetTokenEndpointAsync(cancellationToken);
                (_cachedToken, _tokenExpiry) = await AcquireTokenAsync(endpoint, cancellationToken);
                _log.CognitoTokenService_TokenAcquired();
                return _cachedToken;
            }
            catch (Exception ex)
            {
                _log.CognitoTokenService_AcquireTokenFailed(ex);
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string> GetTokenEndpointAsync(CancellationToken cancellationToken)
    {
        if (_tokenEndpoint is not null)
        {
            return _tokenEndpoint;
        }

        CognitoClientSettings cognito = _settings.Cognito;
        string discoveryUrl = $"{cognito.Authority.TrimEnd('/')}/.well-known/openid-configuration";

        using HttpResponseMessage discoveryResponse =
            await _httpClient.GetAsync(discoveryUrl, cancellationToken);

        discoveryResponse.EnsureSuccessStatusCode();

        string json = await discoveryResponse.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument doc = JsonDocument.Parse(json);
        _tokenEndpoint = doc.RootElement.GetProperty("token_endpoint").GetString()
            ?? throw new InvalidOperationException(
                "token_endpoint not found in OIDC discovery document");

        return _tokenEndpoint;
    }

    private async Task<(string Token, DateTimeOffset Expiry)> AcquireTokenAsync(
        string endpoint,
        CancellationToken cancellationToken)
    {
        CognitoClientSettings cognito = _settings.Cognito;

        List<KeyValuePair<string, string>> parameters =
        [
            new("grant_type", "client_credentials"),
            new("client_id", cognito.ClientId),
            new("client_secret", cognito.ClientSecret),
        ];

        if (!string.IsNullOrEmpty(cognito.Scope))
        {
            parameters.Add(new("scope", cognito.Scope));
        }

        using FormUrlEncodedContent content = new(parameters);
        using HttpResponseMessage response =
            await _httpClient.PostAsync(endpoint, content, cancellationToken);

        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument doc = JsonDocument.Parse(json);

        string accessToken = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("access_token not found in token response");

        int expiresIn = doc.RootElement.TryGetProperty("expires_in", out JsonElement expiresInElement)
            ? expiresInElement.GetInt32()
            : 3600;

        return (accessToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _lock.Dispose();
    }
}
