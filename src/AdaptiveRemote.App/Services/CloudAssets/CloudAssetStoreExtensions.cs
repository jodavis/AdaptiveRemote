using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.CloudAssets;

/// <summary>
/// Extension methods for ICloudAssetStore that encapsulate asset names for common assets.
/// </summary>
internal static class CloudAssetStoreExtensions
{
    private const string LayoutAssetName = "layout";

    /// <summary>
    /// Retrieves the layout asset from the store.
    /// </summary>
    /// <param name="store">The cloud asset store</param>
    /// <returns>The layout group</returns>
    /// <exception cref="InvalidOperationException">Thrown if layout not found or wrong type</exception>
    public static LayoutGroup GetLayout(this ICloudAssetStore store)
    {
        return store.Get<LayoutGroup>(LayoutAssetName);
    }

    /// <summary>
    /// Stores the layout asset in the store.
    /// </summary>
    /// <param name="store">The cloud asset store</param>
    /// <param name="layout">The layout group to store</param>
    public static void SetLayout(this ICloudAssetStore store, LayoutGroup layout)
    {
        store.Set(LayoutAssetName, layout);
    }
}
