namespace AdaptiveRemote.Backend.RawLayoutService.Configuration;

/// <summary>
/// Configuration for AWS Cognito JWT validation.
/// Maps to the "Cognito" section in appsettings.json.
/// </summary>
public class CognitoSettings
{
    /// <summary>
    /// The Cognito user pool authority URL, e.g.
    /// https://cognito-idp.{region}.amazonaws.com/{userPoolId}
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// The OAuth2 audience (app client ID). If empty, audience validation is skipped.
    /// </summary>
    public string? Audience { get; set; }
}
