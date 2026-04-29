using FluentAssertions;

namespace AdaptiveRemote.Services.CloudAssets;

[TestClass]
public class FileCloudAssetDownloaderTests
{
    private const string FilePath = "dev/layout.json";
    private const string ResourcePath = "/layouts/compiled";

    private static FileSystemCloudAssetDownloader MakeSut(
        string stubFilePath, MockFileSystem fileSystem) =>
        new(new MockOptions<CloudSettings>(new CloudSettings { StubFilePath = stubFilePath }),
            fileSystem.Object);

    [TestMethod]
    public async Task FileCloudAssetDownloader_GetActiveAsync_ReturnsStreamForConfiguredPathAsync()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        fileSystem.AddFile(FilePath, "{}");
        FileSystemCloudAssetDownloader sut = MakeSut(FilePath, fileSystem);

        // Act
        Stream? result = await sut.GetActiveAsync(ResourcePath, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [TestMethod]
    public async Task FileCloudAssetDownloader_GetActiveAsync_ReturnsNullWhenFileAbsentAsync()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        FileSystemCloudAssetDownloader sut = MakeSut("nonexistent/layout.json", fileSystem);

        // Act
        Stream? result = await sut.GetActiveAsync(ResourcePath, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public async Task FileCloudAssetDownloader_GetByIdAsync_ReturnsNullAsync()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        fileSystem.AddFile(FilePath, "{}");
        FileSystemCloudAssetDownloader sut = MakeSut(FilePath, fileSystem);

        // Act
        Stream? result = await sut.GetByIdAsync(ResourcePath, Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
