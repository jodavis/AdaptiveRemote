using System.Runtime.InteropServices.Marshalling;
using System.Security.Cryptography;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using AdaptiveRemote.Models.CloudAssets;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.CloudAssets;

internal class CloudAssetOrchestrator : BackgroundService, IPreScopeInitializer
{
    private readonly IEnumerable<ICloudAsset> _assets;
    private readonly ICloudAssetDownloader _downloader;
    private readonly ICloudAssetStore _store;
    private readonly ICloudAssetCache _cache;
    private readonly IApplicationRecycleSignal _signal;
    private readonly IIdleDetector _idleDetector;
    private readonly ICloudAssetChangeNotifier _changeNotifier;
    private readonly MessageLogger _log;
    private readonly TaskCompletionSource _initCompleted = new();

    // SHA256 hashes of bytes last written to cache, keyed by asset name.
    // Populated only for assets loaded from cache in Phase 1; used by Phase 2 to detect server changes.
    private readonly Dictionary<string, byte[]> _cacheHashes = new();

    // Non-null while a WaitForIdleAsync task is pending; prevents stacking recycle requests
    // across Phase 2/3 cycles.
    private Task? _pendingRecycleTask;

    public CloudAssetOrchestrator(
        IEnumerable<ICloudAsset> assets,
        ICloudAssetDownloader downloader,
        ICloudAssetStore store,
        ICloudAssetCache cache,
        IApplicationRecycleSignal signal,
        IIdleDetector idleDetector,
        ICloudAssetChangeNotifier changeNotifier,
        ILogger<CloudAssetOrchestrator> logger)
    {
        _assets = assets;
        _downloader = downloader;
        _store = store;
        _cache = cache;
        _signal = signal;
        _idleDetector = idleDetector;
        _changeNotifier = changeNotifier;
        _log = new MessageLogger(logger);
    }

    public string Name => "Loading cloud assets";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Phase1Async(stoppingToken);
            _initCompleted.SetResult();
        }
        catch (Exception ex)
        {
            _log.CloudAssetOrchestrator_Failed(ex);
            _initCompleted.TrySetException(ex);
            // Do not re-throw: ApplicationLifecycle observes the faulted WaitAsync and sets
            // FatalError. Re-throwing here would trigger BackgroundServiceExceptionBehavior.StopHost,
            // which kills the process before the FatalError UI state can be observed.
            return;
        }

        try
        {
            await Phase2Async(stoppingToken);
            await Phase3Async(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown — swallow so BackgroundServiceExceptionBehavior.StopHost is not triggered.
        }
        catch (Exception ex)
        {
            // Unexpected exception in background phases — log and exit cleanly.
            _log.CloudAssetOrchestrator_Failed(ex);
        }
    }

    public Task WaitAsync(ILifecycleActivity activity, CancellationToken ct)
    {
        activity.Description = Phrases.Startup_LoadingCloudAssets;
        return _initCompleted.Task.WaitAsync(ct);
    }

    private async Task Phase1Async(CancellationToken ct)
    {
        ICloudAsset[] assets = _assets.ToArray();
        await Task.WhenAll(assets.Select(asset => LoadAssetAsync(asset, ct)));
    }

    private async Task LoadAssetAsync(ICloudAsset asset, CancellationToken ct)
    {
        byte[]? cachedBytes = await LoadAssetFromCacheAsync(asset, ct);
        if (cachedBytes != null)
        {
            await ApplyAssetAsync(asset, cachedBytes, fromCache: true, ct);
            _log.CloudAssetOrchestrator_LoadedFromCache(asset.Name);
            return;
        }
        else
        {
            _log.CloudAssetOrchestrator_NotFoundInCache(asset.Name);
        }

        byte[] serverBytes = await DownloadAssetAsync(asset, ct)
            ?? throw new InvalidOperationException($"Failed to download asset '{asset.Name}'.");

        await ApplyAssetAsync(asset, serverBytes, ct);
    }

    private async Task<byte[]?> LoadAssetFromCacheAsync(ICloudAsset asset, CancellationToken ct)
    {
        Stream? cachedStream = await _cache.LoadAsync(asset.Name, ct);
        if (cachedStream != null)
        {
            return await ReadAllBytesAndDisposeAsync(cachedStream, ct);
        }

        return null;
    }

    private async Task Phase2Async(CancellationToken ct)
    {
        // Only check assets that were loaded from cache in Phase 1 (present in _cacheHashes).
        HashSet<string> cacheLoadedNames;
        lock (_cacheHashes)
        {
            cacheLoadedNames = [.. _cacheHashes.Keys];
        }
        ICloudAsset[] toCheck = _assets.Where(a => cacheLoadedNames.Contains(a.Name)).ToArray();

        foreach (ICloudAsset asset in toCheck)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            byte[]? serverBytes = await DownloadAssetAsync(asset, ct);
            ct.ThrowIfCancellationRequested();

            if (serverBytes == null)
            {
                continue;
            }

            await ApplyAssetAsync(asset, serverBytes, ct);
            ct.ThrowIfCancellationRequested();
        }
    }

    private async Task Phase3Async(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            ICloudAsset asset = await _changeNotifier.WaitForChangeAsync(ct);
            ct.ThrowIfCancellationRequested();

            _log.CloudAssetOrchestrator_FileChangeDetected(asset.Name);

            byte[]? serverBytes = await DownloadAssetAsync(asset, ct);
            ct.ThrowIfCancellationRequested();

            if (serverBytes is not null)
            {
                await ApplyAssetAsync(asset, serverBytes, ct);
            }
        }
    }

    private static async Task<byte[]> ReadAllBytesAndDisposeAsync(Stream stream, CancellationToken ct)
    {
        await using (stream)
        {
            using MemoryStream ms = new();
            await stream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }
    }

    private Task ApplyAssetAsync(ICloudAsset asset, byte[] bytes, CancellationToken ct = default)
        => ApplyAssetAsync(asset, bytes, fromCache: false, ct);

    private async Task ApplyAssetAsync(ICloudAsset asset, byte[] bytes, bool fromCache, CancellationToken ct = default)
    {
        byte[] assetHash = SHA256.HashData(bytes);
        if (ShouldApply(asset, assetHash))
        {
            object value = await asset.DeserializeAsync(new MemoryStream(bytes), ct);
            _store.Set(asset.Name, value);

            if (fromCache)
            {
                _cacheHashes[asset.Name] = assetHash;
            }
            else
            {
                await _cache.SaveAsync(asset.Name, new MemoryStream(bytes), ct);
            }

            if (_initCompleted.Task.IsCompleted)
            {
                // Only need to recycle if we've already signaled that initialization is complete,
                // otherwise the initialization sequence will take care of applying the assets.
                _log.CloudAssetOrchestrator_AssetUpdated(asset.Name);

                IdleDeferRecycle();
            }
        }
    }

    private bool ShouldApply(ICloudAsset asset, byte[] assetHash)
    {
        lock (_cacheHashes)
        {
            if (_cacheHashes.TryGetValue(asset.Name, out byte[]? cachedHash)
                && assetHash.AsSpan().SequenceEqual(cachedHash))
            {
                _log.CloudAssetOrchestrator_AssetUpToDate(asset.Name);
                return false;
            }
        }

        return true;
    }

    private async Task<byte[]?> DownloadAssetAsync(ICloudAsset asset, CancellationToken ct)
    {
        _log.CloudAssetOrchestrator_Downloading(asset.Name);

        Stream? serverStream;
        try
        {
            serverStream = await _downloader.GetActiveAsync(asset.ResourcePath, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.CloudAssetOrchestrator_BackgroundFetchFailed(asset.Name, ex);
            return null;
        }

        if (serverStream == null)
        {
            _log.CloudAssetOrchestrator_BackgroundFetchFailed(asset.Name, null);
            return null;
        }

        byte[] bytes = await ReadAllBytesAndDisposeAsync(serverStream, ct);
        _log.CloudAssetOrchestrator_Downloaded(asset.Name);

        return bytes;
    }

    private void IdleDeferRecycle()
    {
        if (_pendingRecycleTask?.IsCompleted == false)
        {
            return;
        }

        _pendingRecycleTask = _idleDetector.WaitForIdleAsync(default)
            .ContinueWith(
                _ => _signal.RequestRecycle(),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
    }
}

