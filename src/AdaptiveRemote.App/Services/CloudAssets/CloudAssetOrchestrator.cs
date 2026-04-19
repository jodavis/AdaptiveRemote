using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.CloudAssets;

internal class CloudAssetOrchestrator : BackgroundService, IPreScopeInitializer
{
    private readonly IEnumerable<ICloudAsset> _assets;
    private readonly ICloudAssetDownloader _downloader;
    private readonly ICloudAssetStore _store;
    private readonly TaskCompletionSource _initCompleted = new();

    public CloudAssetOrchestrator(
        IEnumerable<ICloudAsset> assets,
        ICloudAssetDownloader downloader,
        ICloudAssetStore store)
    {
        _assets = assets;
        _downloader = downloader;
        _store = store;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            foreach (ICloudAsset asset in _assets)
            {
                Stream stream = await _downloader.GetActiveAsync(asset.ResourcePath, stoppingToken)
                    ?? throw new InvalidOperationException($"Failed to download asset '{asset.Name}'.");
                await using (stream)
                {
                    object value = await asset.ParseAsync(stream, stoppingToken);
                    _store.Set(asset.Name, value);
                }
            }
            _initCompleted.SetResult();
        }
        catch (Exception ex)
        {
            _initCompleted.TrySetException(ex);
            throw;
        }
    }

    public Task WaitAsync(ILifecycleActivity activity, CancellationToken ct)
    {
        activity.Description = "Loading cloud assets";
        return _initCompleted.Task.WaitAsync(ct);
    }
}
