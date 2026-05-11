namespace AdaptiveRemote.Services.CloudAssets;

/// <summary>
/// Acquires and caches OAuth2 access tokens for authenticating requests to cloud asset
/// backend services. Callers receive a valid bearer token without managing token expiry.
/// </summary>
internal interface ICloudAuthTokenProvider
{
    /// <summary>
    /// Returns a valid access token, acquiring or refreshing it from the identity provider
    /// as needed. Returns <see langword="null"/> when cloud credentials are not configured.
    /// </summary>
    Task<string?> GetTokenAsync(CancellationToken ct);
}
