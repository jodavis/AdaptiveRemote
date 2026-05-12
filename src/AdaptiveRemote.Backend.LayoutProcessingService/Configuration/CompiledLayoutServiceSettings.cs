namespace AdaptiveRemote.Backend.LayoutProcessingService.Configuration;

/// <summary>
/// Configuration for HTTP communication with CompiledLayoutService.
/// Maps to the "CompiledLayoutService" section in appsettings.json.
/// </summary>
public class CompiledLayoutServiceSettings
{
    /// <summary>
    /// Base URL of CompiledLayoutService, e.g. http://compiledlayoutservice:8080
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
