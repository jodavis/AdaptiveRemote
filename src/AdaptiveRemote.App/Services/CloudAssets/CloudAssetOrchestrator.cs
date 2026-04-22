using AdaptiveRemote.Logging;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.CloudAssets;

internal class CloudAssetOrchestrator : BackgroundService, IPreScopeInitializer
{
    private readonly IEnumerable<ICloudAsset> _assets;
    private readonly ICloudAssetDownloader _downloader;
    private readonly ICloudAssetStore _store;
    private readonly MessageLogger _log;
    private readonly TaskCompletionSource _initCompleted = new();

    public CloudAssetOrchestrator(
        IEnumerable<ICloudAsset> assets,
        ICloudAssetDownloader downloader,
        ICloudAssetStore store,
        ILogger<CloudAssetOrchestrator> logger)
    {
        _assets = assets;
        _downloader = downloader;
        _store = store;
        _log = new MessageLogger(logger);
    }

    public string Name => "Loading cloud assets";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            foreach (ICloudAsset asset in _assets)
            {
                _log.CloudAssetOrchestrator_Downloading(asset.Name);
                Stream stream = await _downloader.GetActiveAsync(asset.ResourcePath, stoppingToken)
                    ?? throw new InvalidOperationException($"Failed to download asset '{asset.Name}'.");
                await using (stream)
                {
                    object value = await asset.DeserializeAsync(stream, stoppingToken);
                    _store.Set(asset.Name, value);
                }
            }
            _initCompleted.SetResult();
        }
        catch (Exception ex)
        {
            _log.CloudAssetOrchestrator_Failed(ex);
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
