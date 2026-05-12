using System.Collections.Concurrent;

namespace AdaptiveRemote.Services.CloudAssets;

/// <summary>
/// Thread-safe in-memory dictionary keyed by asset name.
/// </summary>
internal class CloudAssetStore : ICloudAssetStore
{
    private readonly ConcurrentDictionary<string, object> _assets = new();

    public T Get<T>(string name)
    {
        if (!_assets.TryGetValue(name, out object? value))
        {
            throw new InvalidOperationException($"Asset '{name}' not found in store. Ensure CloudAssetOrchestrator has completed before accessing assets.");
        }

        if (value is not T typedValue)
        {
            throw new InvalidOperationException($"Asset '{name}' is of type '{value.GetType().Name}', but '{typeof(T).Name}' was requested.");
        }

        return typedValue;
    }

    public void Set(string name, object asset)
    {
        _assets[name] = asset;
    }
}
