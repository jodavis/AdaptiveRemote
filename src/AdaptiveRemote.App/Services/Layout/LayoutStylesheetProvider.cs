using System.Reflection;

namespace AdaptiveRemote.Services.Layout;

/// <summary>
/// LayoutStylesheetProvider v1: Returns static grid CSS extracted from app.css.
/// This will be replaced in a future task to read CSS from CompiledLayout.
/// </summary>
internal class LayoutStylesheetProvider : IDynamicStylesheetProvider
{
    private static readonly Lazy<string> _gridCss = new(LoadGridCss);

    public string? GetCss() => _gridCss.Value;

    private static string LoadGridCss()
    {
        Assembly assembly = typeof(LayoutStylesheetProvider).Assembly;
        string resourceName = "AdaptiveRemote.wwwroot.css.layout-grid.css";

        using Stream? stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found in assembly '{assembly.FullName}'.");

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
