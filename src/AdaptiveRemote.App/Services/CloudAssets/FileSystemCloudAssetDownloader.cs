using AdaptiveRemote.Services;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.CloudAssets;

internal sealed class FileSystemCloudAssetDownloader : ICloudAssetDownloader
{
    private readonly CloudSettings _settings;
    private readonly IFileSystem _fileSystem;
    private readonly ICloudAuthTokenProvider _tokenProvider;

    public FileSystemCloudAssetDownloader(
        IOptions<CloudSettings> options,
        IFileSystem fileSystem,
        ICloudAuthTokenProvider tokenProvider)
    {
        _settings = options.Value;
        _fileSystem = fileSystem;
        _tokenProvider = tokenProvider;
    }

    public async Task<Stream?> GetActiveAsync(string resourcePath, CancellationToken ct)
    {
        string path = Environment.ExpandEnvironmentVariables(_settings.StubFilePath);
        if (!_fileSystem.FileExists(path))
        {
            return null;
        }

        _ = await _tokenProvider.GetTokenAsync(ct);
        return _fileSystem.OpenRead(path);
    }

    public Task<Stream?> GetByIdAsync(string resourcePath, Guid id, CancellationToken ct)
        => Task.FromResult<Stream?>(null);
}
