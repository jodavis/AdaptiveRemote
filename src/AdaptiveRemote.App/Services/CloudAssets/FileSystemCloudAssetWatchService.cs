using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.CloudAssets;

internal sealed class FileSystemCloudAssetWatchService : BackgroundService, IAssetChangeNotifier
{
    private readonly CloudSettings _settings;
    private readonly SemaphoreSlim _semaphore = new(0, 1);
    private readonly object _debounceLock = new();
    private CancellationTokenSource? _debounceCts;

    public FileSystemCloudAssetWatchService(IOptions<CloudSettings> options)
    {
        _settings = options.Value;
    }

    public async Task<string> WaitForChangeAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        return _settings.AssetName;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string path = Environment.ExpandEnvironmentVariables(_settings.StubFilePath);
        string? dir = Path.GetDirectoryName(path);
        string fileName = Path.GetFileName(path);

        if (dir is null || fileName is null)
        {
            return Task.CompletedTask;
        }

        FileSystemWatcher watcher = new(dir, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Renamed += OnChanged;

        stoppingToken.Register(watcher.Dispose);

        return Task.CompletedTask;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        CancellationTokenSource newCts;
        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            newCts = _debounceCts = new CancellationTokenSource();
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(100, newCts.Token);
                try
                {
                    _semaphore.Release();
                }
                catch (SemaphoreFullException)
                {
                    // Already a notification pending — swallow so the count stays at 1.
                }
            }
            catch (OperationCanceledException)
            {
                // Debounced by a later event.
            }
        });
    }
}
