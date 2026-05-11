using System.Text.Json;
using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.CloudAssets;

/// <summary>
/// Acquires and caches OAuth2 access tokens from AWS Cognito using the Client Credentials
/// flow. Token refresh is lazy: the cached token is returned until it is within
/// <see cref="ExpiryBuffer"/> of expiring, at which point a new token is acquired.
/// </summary>
internal sealed class CognitoTokenProvider : ICloudAuthTokenProvider, IDisposable
{
    // Refresh the token this many seconds before it actually expires.
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromSeconds(60);

    private readonly CloudSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly MessageLogger _log;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _missingConfigurationLogged;

    public CognitoTokenProvider(
        IOptions<CloudSettings> options,
        ILogger<CognitoTokenProvider> logger)
    {
        _settings = options.Value;
        _httpClient = new HttpClient();
        _log = new MessageLogger(logger);
    }

    // Internal constructor for unit testing — allows injection of a custom HttpMessageHandler.
    internal CognitoTokenProvider(
        IOptions<CloudSettings> options,
        ILogger<CognitoTokenProvider> logger,
        HttpMessageHandler httpMessageHandler)
    {
        _settings = options.Value;
        _httpClient = new HttpClient(httpMessageHandler);
        _log = new MessageLogger(logger);
    }

    public async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!IsConfigured())
            {
                if (!_missingConfigurationLogged)
                {
                    _log.CognitoTokenProvider_MissingConfiguration();
                    _missingConfigurationLogged = true;
                }

                return null;
            }

            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry - ExpiryBuffer)
            {
                return _cachedToken;
            }

            _log.CognitoTokenProvider_AcquiringToken();
            try
            {
                (_cachedToken, _tokenExpiry) = await AcquireTokenAsync(ct);
                _log.CognitoTokenProvider_TokenAcquired();
                return _cachedToken;
            }
            catch (Exception ex)
            {
                _log.CognitoTokenProvider_AcquireTokenFailed(ex);
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<(string Token, DateTimeOffset Expiry)> AcquireTokenAsync(
        CancellationToken ct)
    {
        List<KeyValuePair<string, string>> parameters =
        [
            new("grant_type", "client_credentials"),
            new("client_id", _settings.ClientId),
            new("client_secret", _settings.ClientSecret),
        ];

        using FormUrlEncodedContent content = new(parameters);
        using HttpResponseMessage response =
            await _httpClient.PostAsync(_settings.CognitoTokenEndpointUrl, content, ct);

        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct);
        using JsonDocument doc = JsonDocument.Parse(json);

        string accessToken = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("access_token not found in token response");

        int expiresIn = doc.RootElement.TryGetProperty("expires_in", out JsonElement expiresInElement)
            ? expiresInElement.GetInt32()
            : 3600;

        return (accessToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }

    private bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_settings.ClientId)
        && !string.IsNullOrWhiteSpace(_settings.ClientSecret)
        && !string.IsNullOrWhiteSpace(_settings.CognitoTokenEndpointUrl);

    public void Dispose()
    {
        _httpClient.Dispose();
        _lock.Dispose();
    }
}
