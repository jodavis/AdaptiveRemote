using System.Security.Cryptography;
using AdaptiveRemote.Logging;
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
        activity.Description = "Loading cloud assets";
        return _initCompleted.Task.WaitAsync(ct);
    }

    private async Task Phase1Async(CancellationToken ct)
    {
        ICloudAsset[] assets = _assets.ToArray();
        await Task.WhenAll(assets.Select(asset => LoadAssetAsync(asset, ct)));
    }

    private async Task LoadAssetAsync(ICloudAsset asset, CancellationToken ct)
    {
        Stream? cachedStream = await _cache.LoadAsync(asset.Name, ct);
        if (cachedStream != null)
        {
            byte[] cachedBytes;
            await using (cachedStream)
            {
                cachedBytes = await ReadAllBytesAsync(cachedStream, ct);
            }
            object value = await asset.DeserializeAsync(new MemoryStream(cachedBytes), ct);
            _store.Set(asset.Name, value);
            _log.CloudAssetOrchestrator_LoadedFromCache(asset.Name);
            lock (_cacheHashes)
            {
                _cacheHashes[asset.Name] = SHA256.HashData(cachedBytes);
            }
            return;
        }

        _log.CloudAssetOrchestrator_Downloading(asset.Name);
        Stream serverStream = await _downloader.GetActiveAsync(asset.ResourcePath, ct)
            ?? throw new InvalidOperationException($"Failed to download asset '{asset.Name}'.");

        byte[] serverBytes;
        await using (serverStream)
        {
            serverBytes = await ReadAllBytesAsync(serverStream, ct);
        }
        object serverValue = await asset.DeserializeAsync(new MemoryStream(serverBytes), ct);
        await _cache.SaveAsync(asset.Name, new MemoryStream(serverBytes), ct);
        _store.Set(asset.Name, serverValue);
    }

    private async Task Phase2Async(CancellationToken ct)
    {
        // Only check assets that were loaded from cache in Phase 1 (present in _cacheHashes).
        ICloudAsset[] allAssets = _assets.ToArray();
        HashSet<string> cacheLoadedNames;
        lock (_cacheHashes)
        {
            cacheLoadedNames = [.._cacheHashes.Keys];
        }
        ICloudAsset[] toCheck = allAssets.Where(a => cacheLoadedNames.Contains(a.Name)).ToArray();

        foreach (ICloudAsset asset in toCheck)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            Stream? serverStream;
            try
            {
                serverStream = await _downloader.GetActiveAsync(asset.ResourcePath, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.CloudAssetOrchestrator_BackgroundFetchFailed(asset.Name, ex);
                continue;
            }

            if (serverStream == null)
            {
                _log.CloudAssetOrchestrator_BackgroundFetchFailed(asset.Name, null);
                continue;
            }

            byte[] serverBytes;
            try
            {
                await using (serverStream)
                {
                    serverBytes = await ReadAllBytesAsync(serverStream, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await ApplyServerUpdateAsync(asset, serverBytes, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task Phase3Async(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string changedAssetName;
            try
            {
                changedAssetName = await _changeNotifier.WaitForChangeAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }

            ICloudAsset? asset = _assets.FirstOrDefault(a => a.Name == changedAssetName);
            if (asset is null)
            {
                _log.CloudAssetOrchestrator_UnknownAssetChange(changedAssetName);
                continue;
            }

            if (ct.IsCancellationRequested)
            {
                continue;
            }

            _log.CloudAssetOrchestrator_FileChangeDetected(asset.Name);

            Stream? serverStream;
            try
            {
                serverStream = await _downloader.GetActiveAsync(asset.ResourcePath, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.CloudAssetOrchestrator_BackgroundFetchFailed(asset.Name, ex);
                continue;
            }

            if (serverStream == null)
            {
                _log.CloudAssetOrchestrator_BackgroundFetchFailed(asset.Name, null);
                continue;
            }

            try
            {
                byte[] serverBytes;
                await using (serverStream)
                {
                    serverBytes = await ReadAllBytesAsync(serverStream, ct);
                }
                await ApplyServerUpdateAsync(asset, serverBytes, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Compares <paramref name="serverBytes"/> against the cached hash for the asset.
    /// If the content differs (or has no cached hash), deserializes, saves to cache,
    /// updates the store, and schedules an idle-deferred recycle.
    /// </summary>
    private async Task ApplyServerUpdateAsync(ICloudAsset asset, byte[] serverBytes, CancellationToken ct)
    {
        byte[] serverHash = SHA256.HashData(serverBytes);

        lock (_cacheHashes)
        {
            if (_cacheHashes.TryGetValue(asset.Name, out byte[]? cachedHash)
                && serverHash.AsSpan().SequenceEqual(cachedHash))
            {
                _log.CloudAssetOrchestrator_AssetUpToDate(asset.Name);
                return;
            }
        }

        object value = await asset.DeserializeAsync(new MemoryStream(serverBytes), ct);
        await _cache.SaveAsync(asset.Name, new MemoryStream(serverBytes), ct);
        _store.Set(asset.Name, value);
        lock (_cacheHashes)
        {
            _cacheHashes[asset.Name] = serverHash;
        }
        _log.CloudAssetOrchestrator_AssetUpdated(asset.Name);
        IdleDeferRecycle();
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

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken ct)
    {
        using MemoryStream ms = new();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}

