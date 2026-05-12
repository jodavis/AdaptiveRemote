using AdaptiveRemote.Services;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.CloudAssets;

internal sealed class CloudAssetCache : ICloudAssetCache
{
    private readonly string _cacheDirectory;
    private readonly IFileSystem _fileSystem;

    public CloudAssetCache(IOptions<CloudSettings> options, IFileSystem fileSystem)
    {
        _cacheDirectory = Environment.ExpandEnvironmentVariables(options.Value.CachePath);
        _fileSystem = fileSystem;
    }

    public Task<Stream?> LoadAsync(string name, CancellationToken ct)
    {
        string path = GetCachePath(name);
        if (!_fileSystem.FileExists(path))
        {
            return Task.FromResult<Stream?>(null);
        }
        return Task.FromResult<Stream?>(_fileSystem.OpenRead(path));
    }

    public async Task SaveAsync(string name, Stream assetData, CancellationToken ct)
    {
        string path = GetCachePath(name);
        await using Stream dest = _fileSystem.OpenWrite(path, createDirectory: true);
        await assetData.CopyToAsync(dest, ct);
    }

    private string GetCachePath(string name)
    {
        // Asset names are developer-controlled DI constants, but guard against accidental
        // misconfiguration that would place a cache file outside the cache directory.
        if (name.IndexOfAny(['/', '\\', ':']) >= 0 || name.StartsWith('.'))
        {
            throw new ArgumentException($"Invalid asset name '{name}'.", nameof(name));
        }
        return Path.Combine(_cacheDirectory, $"{name}.cache");
    }
}
