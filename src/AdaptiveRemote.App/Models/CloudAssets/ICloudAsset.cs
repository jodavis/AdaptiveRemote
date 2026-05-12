namespace AdaptiveRemote.Models.CloudAssets;

/// <summary>
/// Per-asset capability bundle. One implementation per cloud-fetched asset type.
/// </summary>
internal interface ICloudAsset
{
    /// <summary>
    /// Unique key used in store/cache; also used for logging
    /// </summary>
    string Name { get; }

    /// <summary>
    /// SSE endpoint, e.g. "/notifications/layouts/stream"
    /// </summary>
    string StreamUrl { get; }

    /// <summary>
    /// SSE event name, e.g. "layout-ready"
    /// </summary>
    string EventName { get; }

    /// <summary>
    /// REST base path, e.g. "/layouts/compiled"
    /// </summary>
    string ResourcePath { get; }

    /// <summary>
    /// Deserializes downloaded or cached bytes into the asset's runtime type.
    /// </summary>
    Task<object> DeserializeAsync(Stream stream, CancellationToken ct);
}

/// <summary>
/// Marker interface — allows type-constrained DI registrations:
///   services.AddScoped(sp => sp.GetRequiredService&lt;ICloudAssetStore&gt;().Get&lt;T&gt;(name))
/// </summary>
internal interface ICloudAsset<T> : ICloudAsset { }
