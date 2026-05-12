namespace AdaptiveRemote.Services;

/// <summary>
/// Scoped. Returns the CSS for the active layout in this scope.
/// </summary>
public interface IDynamicStylesheetProvider
{
    string? GetCss();
}
