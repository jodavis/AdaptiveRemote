using AdaptiveRemote.Contracts;
using AdaptiveRemote.Services.CloudAssets;

namespace AdaptiveRemote.Services.Layout;

internal sealed class LayoutStylesheetProvider : IDynamicStylesheetProvider
{
    private readonly ICloudAssetStore _store;

    public LayoutStylesheetProvider(ICloudAssetStore store)
    {
        _store = store;
    }

    public string? GetCss()
    {
        CompiledLayout layout = _store.Get<CompiledLayout>("layout");
        return layout.CssDefinitions;
    }
}
