using AdaptiveRemote.Services;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.CloudAssets;

internal sealed class FileSystemCloudAssetDownloader : ICloudAssetDownloader
{
    private readonly CloudSettings _settings;
    private readonly IFileSystem _fileSystem;

    public FileSystemCloudAssetDownloader(IOptions<CloudSettings> options, IFileSystem fileSystem)
    {
        _settings = options.Value;
        _fileSystem = fileSystem;
    }

    public Task<Stream?> GetActiveAsync(string resourcePath, CancellationToken ct)
    {
        string path = Environment.ExpandEnvironmentVariables(_settings.StubFilePath);
        if (!_fileSystem.FileExists(path))
        {
            return Task.FromResult<Stream?>(null);
        }
        return Task.FromResult<Stream?>(_fileSystem.OpenRead(path));
    }

    public Task<Stream?> GetByIdAsync(string resourcePath, Guid id, CancellationToken ct)
        => Task.FromResult<Stream?>(null);
}
