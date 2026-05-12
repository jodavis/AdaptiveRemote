namespace AdaptiveRemote.Backend.LayoutProcessingService.Configuration;

/// <summary>
/// Configuration for HTTP communication with RawLayoutService.
/// Maps to the "RawLayoutService" section in appsettings.json.
/// </summary>
public class RawLayoutServiceSettings
{
    /// <summary>
    /// Base URL of RawLayoutService, e.g. http://rawlayoutservice:8080
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional bearer token for service-to-service authentication.
    /// In production, this will be replaced by IAM-signed requests or a Cognito
    /// machine-to-machine token. For local development and testing, set this to
    /// a valid JWT from the same authority as RawLayoutService.
    /// </summary>
    public string? ServiceAccountToken { get; set; }
}
