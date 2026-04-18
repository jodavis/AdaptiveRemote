using System.Reflection;

namespace AdaptiveRemote.Services.Layout;

internal sealed class LayoutStylesheetProvider : IDynamicStylesheetProvider
{
    private static readonly string _css = LoadCss();

    public string? GetCss() => _css;

    private static string LoadCss()
    {
        Assembly assembly = typeof(LayoutStylesheetProvider).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(
            "AdaptiveRemote.Services.Layout.layout-grid.css")!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
