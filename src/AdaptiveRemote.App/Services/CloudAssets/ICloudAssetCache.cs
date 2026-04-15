namespace AdaptiveRemote.Services.CloudAssets;

/// <summary>
/// File-backed persistence for raw asset bytes, keyed by Name.
/// </summary>
internal interface ICloudAssetCache
{
    /// <summary>
    /// Loads an asset from the cache.
    /// </summary>
    /// <param name="name">The unique name of the asset</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A stream containing the cached asset data, or null if no cached file exists</returns>
    Task<Stream?> LoadAsync(string name, CancellationToken ct);

    /// <summary>
    /// Saves an asset to the cache.
    /// </summary>
    /// <param name="name">The unique name of the asset</param>
    /// <param name="assetData">A stream containing the asset data to cache</param>
    /// <param name="ct">Cancellation token</param>
    Task SaveAsync(string name, Stream assetData, CancellationToken ct);
}
