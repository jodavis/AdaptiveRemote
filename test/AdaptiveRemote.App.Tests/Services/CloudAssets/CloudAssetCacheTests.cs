using System.Text;
using AdaptiveRemote.TestUtilities;
using FluentAssertions;

namespace AdaptiveRemote.Services.CloudAssets;

[TestClass]
public class CloudAssetCacheTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private const string AssetName = "layout";
    private const string CachePath = "cache";
    private static readonly string CacheFile = Path.Combine("cache", "layout.cache");
    private const string CacheDir = CachePath;

    private static CloudAssetCache MakeSut(MockFileSystem fileSystem, string cachePath = CachePath)
        => new(new MockOptions<CloudSettings>(new CloudSettings { CachePath = cachePath }), fileSystem.Object);

    [TestMethod]
    public void CloudAssetCache_SaveAsync_WritesFileWithCorrectContent()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        fileSystem.AddDirectory(CacheDir);
        byte[] content = "test-payload"u8.ToArray();
        CloudAssetCache sut = MakeSut(fileSystem);

        // Act
        sut.SaveAsync(AssetName, new MemoryStream(content), CancellationToken.None)
            .Should().BeCompleteWithin(Timeout);

        // Assert
        fileSystem.VerifyFileContents(CacheFile, "test-payload");
    }

    [TestMethod]
    public void CloudAssetCache_SaveAsync_CreatesDirectoryWhenAbsent()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        fileSystem.Expect_CreateDirectory_ForPath(CacheDir);
        byte[] content = "data"u8.ToArray();
        CloudAssetCache sut = MakeSut(fileSystem);

        // Act
        sut.SaveAsync(AssetName, new MemoryStream(content), CancellationToken.None)
            .Should().BeCompleteWithin(Timeout);

        // Assert
        fileSystem.Verify();
    }

    [TestMethod]
    public void CloudAssetCache_SaveAsync_DoesNotCreateDirectoryWhenPresent()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        fileSystem.AddDirectory(CacheDir);
        fileSystem.Expect_CreateDirectory_IsNotCalled();
        CloudAssetCache sut = MakeSut(fileSystem);

        // Act
        sut.SaveAsync(AssetName, new MemoryStream("data"u8.ToArray()), CancellationToken.None)
            .Should().BeCompleteWithin(Timeout);

        // Assert
        fileSystem.Verify();
    }

    [TestMethod]
    public void CloudAssetCache_LoadAsync_ReturnsStreamOnCacheHit()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        fileSystem.AddFile(CacheFile, "cached-data");
        CloudAssetCache sut = MakeSut(fileSystem);

        // Act
        Task<Stream?> loadTask = sut.LoadAsync(AssetName, CancellationToken.None);
        loadTask.Should().BeCompleteWithin(Timeout);
        Stream? result = loadTask.Result;

        // Assert
        result.Should().NotBeNull();
        using StreamReader reader = new(result!);
        reader.ReadToEnd().Should().Be("cached-data");
    }

    [TestMethod]
    public void CloudAssetCache_LoadAsync_ReturnsNullOnCacheMiss()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        CloudAssetCache sut = MakeSut(fileSystem);

        // Act
        Task<Stream?> loadTask = sut.LoadAsync(AssetName, CancellationToken.None);
        loadTask.Should().BeCompleteWithin(Timeout);
        Stream? result = loadTask.Result;

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void CloudAssetCache_SaveThenLoad_RoundTrips()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        fileSystem.AddDirectory(CacheDir);
        byte[] original = Encoding.UTF8.GetBytes("{\"key\":\"value\"}");
        CloudAssetCache sut = MakeSut(fileSystem);

        // Act
        sut.SaveAsync(AssetName, new MemoryStream(original), CancellationToken.None)
            .Should().BeCompleteWithin(Timeout);
        Task<Stream?> loadTask = sut.LoadAsync(AssetName, CancellationToken.None);
        loadTask.Should().BeCompleteWithin(Timeout);
        Stream? loaded = loadTask.Result;

        // Assert
        loaded.Should().NotBeNull();
        using MemoryStream ms = new();
        loaded!.CopyTo(ms);
        ms.ToArray().Should().Equal(original);
    }
}
