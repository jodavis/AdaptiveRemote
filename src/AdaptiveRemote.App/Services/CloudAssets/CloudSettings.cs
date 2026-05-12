namespace AdaptiveRemote.Services.CloudAssets;

/// <summary>
/// Shared settings for all cloud asset services.
/// </summary>
internal class CloudSettings
{
    public int IdleCooldownSeconds { get; set; } = 30;
    public int SseMaxConsecutiveFailures { get; set; } = 10;
    public string CachePath { get; set; } = @"%LocalAppData%\AdaptiveRemote\CloudAssets";
    public string StubFilePath { get; set; } = "dev/layout.json";

    /// <summary>
    /// OAuth2 client ID for the Client Credentials flow.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth2 client secret. Store in user secrets or environment variables —
    /// never commit to source control.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The Cognito token endpoint URL to POST to for acquiring access tokens.
    /// Example: https://cognito-idp.{region}.amazonaws.com/{userPoolId}/oauth2/token
    /// </summary>
    public string CognitoTokenEndpointUrl { get; set; } = string.Empty;
}
