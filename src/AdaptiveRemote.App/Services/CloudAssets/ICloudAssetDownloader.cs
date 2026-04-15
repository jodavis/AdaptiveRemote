namespace AdaptiveRemote.Services.CloudAssets;

/// <summary>
/// HTTP download against the backend REST API.
/// </summary>
internal interface ICloudAssetDownloader
{
    /// <summary>
    /// Downloads the currently active version of an asset.
    /// </summary>
    /// <param name="resourcePath">The REST resource path for the asset</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A stream containing the asset data, or null if the asset is not available</returns>
    Task<Stream?> GetActiveAsync(string resourcePath, CancellationToken ct);

    /// <summary>
    /// Downloads a specific version of an asset by ID.
    /// </summary>
    /// <param name="resourcePath">The REST resource path for the asset</param>
    /// <param name="id">The unique identifier of the asset version</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A stream containing the asset data, or null if the asset is not available</returns>
    Task<Stream?> GetByIdAsync(string resourcePath, Guid id, CancellationToken ct);
}
