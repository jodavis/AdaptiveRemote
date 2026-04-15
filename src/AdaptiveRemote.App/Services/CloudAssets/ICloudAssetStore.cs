namespace AdaptiveRemote.Services.CloudAssets;

/// <summary>
/// In-memory cross-scope holder for all cloud assets, keyed by Name.
/// </summary>
internal interface ICloudAssetStore
{
    /// <summary>
    /// Retrieves an asset from the store.
    /// </summary>
    /// <typeparam name="T">The expected type of the asset</typeparam>
    /// <param name="name">The unique name of the asset</param>
    /// <returns>The asset value</returns>
    /// <exception cref="InvalidOperationException">Thrown if asset not found or wrong type</exception>
    T Get<T>(string name);

    /// <summary>
    /// Stores an asset in the store.
    /// </summary>
    /// <param name="name">The unique name of the asset</param>
    /// <param name="asset">The asset value to store</param>
    void Set(string name, object asset);
}
