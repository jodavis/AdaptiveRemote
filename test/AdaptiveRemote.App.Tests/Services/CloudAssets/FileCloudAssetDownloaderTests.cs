using FluentAssertions;
using Moq;

namespace AdaptiveRemote.Services.CloudAssets;

[TestClass]
public class FileCloudAssetDownloaderTests
{
    private const string FilePath = "dev/layout.json";
    private const string ResourcePath = "/layouts/compiled";

    private static FileSystemCloudAssetDownloader MakeSut(
        string stubFilePath,
        MockFileSystem fileSystem,
        Mock<ICloudAuthTokenProvider>? tokenProvider = null) =>
        new(
            new MockOptions<CloudSettings>(new CloudSettings { StubFilePath = stubFilePath }),
            fileSystem.Object,
            (tokenProvider ?? MakeDefaultTokenProvider()).Object);

    private static Mock<ICloudAuthTokenProvider> MakeDefaultTokenProvider()
    {
        Mock<ICloudAuthTokenProvider> provider = new();
        provider.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-token");
        return provider;
    }

    [TestMethod]
    public async Task FileCloudAssetDownloader_GetActiveAsync_ReturnsStreamForConfiguredPathAsync()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        fileSystem.AddFile(FilePath, "{}");
        Mock<ICloudAuthTokenProvider> tokenProvider = MakeDefaultTokenProvider();
        FileSystemCloudAssetDownloader sut = MakeSut(FilePath, fileSystem, tokenProvider);

        // Act
        Stream? result = await sut.GetActiveAsync(ResourcePath, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        tokenProvider.Verify(p => p.GetTokenAsync(CancellationToken.None), Times.Once);
    }

    [TestMethod]
    public async Task FileCloudAssetDownloader_GetActiveAsync_ReturnsNullWhenFileAbsentAsync()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        Mock<ICloudAuthTokenProvider> tokenProvider = MakeDefaultTokenProvider();
        FileSystemCloudAssetDownloader sut = MakeSut("nonexistent/layout.json", fileSystem, tokenProvider);

        // Act
        Stream? result = await sut.GetActiveAsync(ResourcePath, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        tokenProvider.Verify(p => p.GetTokenAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task FileCloudAssetDownloader_GetByIdAsync_ReturnsNullAsync()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        fileSystem.AddFile(FilePath, "{}");
        Mock<ICloudAuthTokenProvider> tokenProvider = MakeDefaultTokenProvider();
        FileSystemCloudAssetDownloader sut = MakeSut(FilePath, fileSystem, tokenProvider);

        // Act
        Stream? result = await sut.GetByIdAsync(ResourcePath, Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
        tokenProvider.Verify(p => p.GetTokenAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
