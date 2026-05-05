namespace AdaptiveRemote.Services.Backend;

/// <summary>
/// Configuration for the AdaptiveRemote backend services.
/// Maps to the "backend" section in appsettings.json.
/// </summary>
public class BackendSettings
{
    /// <summary>
    /// Base URL of the backend API (e.g. https://api.adaptiveremote.example.com for
    /// production, or http://localhost:8080 for local development).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Cognito OAuth2 client credentials for the client application.
    /// </summary>
    public CognitoClientSettings Cognito { get; set; } = new();
}

/// <summary>
/// AWS Cognito Client Credentials flow settings for the client application.
/// The client application authenticates as a machine client — no interactive login occurs.
/// Sensitive values (ClientSecret) should be stored in user secrets or environment
/// variables, not checked in to source control.
/// </summary>
public class CognitoClientSettings
{
    /// <summary>
    /// The Cognito user pool authority URL, e.g.
    /// https://cognito-idp.{region}.amazonaws.com/{userPoolId}
    /// Used to discover the token endpoint via OIDC configuration.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// The OAuth2 client ID registered in the Cognito user pool for the client application.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The OAuth2 client secret. Store in user secrets or environment variables —
    /// never commit to source control.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// OAuth2 scope(s) to request, space-separated (e.g. "adaptiveremote/layouts.read").
    /// Leave empty to omit the scope parameter.
    /// </summary>
    public string? Scope { get; set; }
}
