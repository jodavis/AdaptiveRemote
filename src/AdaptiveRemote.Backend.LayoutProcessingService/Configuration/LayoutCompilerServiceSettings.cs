namespace AdaptiveRemote.Backend.LayoutProcessingService.Configuration;

/// <summary>
/// Configuration for HTTP communication with LayoutCompilerService.
/// Maps to the "LayoutCompilerService" section in appsettings.json.
/// </summary>
public class LayoutCompilerServiceSettings
{
    /// <summary>
    /// Base URL of LayoutCompilerService, e.g. http://layoutcompilerservice:8080
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
