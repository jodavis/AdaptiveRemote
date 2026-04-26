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
}
