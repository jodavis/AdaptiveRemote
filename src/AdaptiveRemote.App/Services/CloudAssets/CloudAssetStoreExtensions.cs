using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Services.CloudAssets;

internal static class CloudAssetStoreExtensions
{
    private const string LayoutAssetName = "layout";

    public static CompiledLayout GetLayout(this ICloudAssetStore store)
        => store.Get<CompiledLayout>(LayoutAssetName);

    public static void SetLayout(this ICloudAssetStore store, CompiledLayout layout)
        => store.Set(LayoutAssetName, layout);
}
