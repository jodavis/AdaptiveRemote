namespace AdaptiveRemote.Services.Backend;

/// <summary>
/// Acquires and caches OAuth2 access tokens from AWS Cognito using the
/// Client Credentials flow. The client application calls this to obtain a
/// bearer token before making requests to backend services.
/// </summary>
public interface ICognitoTokenService
{
    /// <summary>
    /// Returns a valid access token, acquiring or refreshing it from Cognito
    /// as needed. The returned token is safe to use as a Bearer token immediately.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
