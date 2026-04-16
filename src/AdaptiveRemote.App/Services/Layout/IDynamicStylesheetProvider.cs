namespace AdaptiveRemote.Services.Layout;

/// <summary>
/// Provides CSS for the active layout in the current scope.
/// </summary>
public interface IDynamicStylesheetProvider
{
    /// <summary>
    /// Gets the CSS for the active layout.
    /// </summary>
    /// <returns>CSS string, or null if no CSS is available.</returns>
    string? GetCss();
}
