namespace AdaptiveRemote.Services.CloudAssets;

/// <summary>
/// Acquires and caches OAuth2 access tokens for authenticating requests to cloud asset
/// backend services. Callers receive a valid bearer token without managing token expiry.
/// </summary>
internal interface ICloudAuthTokenProvider
{
    /// <summary>
    /// Returns a valid access token, acquiring or refreshing it from the identity provider
    /// as needed. The returned token is safe to use as a Bearer token immediately.
    /// </summary>
    Task<string> GetTokenAsync(CancellationToken ct);
}
