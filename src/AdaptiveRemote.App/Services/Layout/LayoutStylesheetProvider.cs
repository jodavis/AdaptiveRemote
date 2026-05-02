using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Services.Layout;

internal sealed class LayoutStylesheetProvider : IDynamicStylesheetProvider
{
    private readonly CompiledLayout _layout;

    public LayoutStylesheetProvider(CompiledLayout layout)
    {
        _layout = layout;
    }

    public string? GetCss()
    {
        return _layout.CssDefinitions;
    }
}
