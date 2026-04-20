using AdaptiveRemote.Services.Lifecycle;
using AdaptiveRemote.TestUtilities;
using FluentAssertions;
using Moq;

namespace AdaptiveRemote.Services.CloudAssets;

[TestClass]
public class CloudAssetOrchestratorTests
{
    private const string AssetName = "test-asset";
    private const string ResourcePath = "/test";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Mock<ICloudAsset> MockAsset = new();
    private readonly Mock<ICloudAssetDownloader> MockDownloader = new();
    private readonly Mock<ICloudAssetStore> MockStore = new();
    private readonly MockLogger<CloudAssetOrchestrator> MockLogger = new();
    private readonly Mock<ILifecycleActivity> MockActivity = new();

    private CloudAssetOrchestrator MakeSut(IEnumerable<ICloudAsset>? assets = null)
        => new(assets ?? [MockAsset.Object], MockDownloader.Object, MockStore.Object, MockLogger);

    [TestInitialize]
    public void SetupMocks()
    {
        MockAsset.SetupGet(a => a.Name).Returns(AssetName);
        MockAsset.SetupGet(a => a.ResourcePath).Returns(ResourcePath);
        MockActivity.SetupSet(a => a.Description = It.IsAny<string>());
    }

    [TestMethod]
    public void CloudAssetOrchestrator_ExecuteAsync_DownloadsAndStoresAsset()
    {
        // Arrange
        object parsedValue = new();
        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        MockAsset.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parsedValue);
        CloudAssetOrchestrator sut = MakeSut();

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        Task waitTask = sut.WaitAsync(MockActivity.Object, CancellationToken.None);

        // Assert
        waitTask.Should().BeCompleteWithin(Timeout);
        waitTask.Should().BeSuccessful();
        MockStore.Verify(s => s.Set(AssetName, parsedValue), Times.Once);
        MockLogger.VerifyMessages(log => { log.CloudAssetOrchestrator_Downloading(AssetName); });
    }

    [TestMethod]
    public void CloudAssetOrchestrator_ExecuteAsync_FaultsWhenDownloadReturnsNull()
    {
        // Arrange
        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);
        CloudAssetOrchestrator sut = MakeSut();

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        Task waitTask = sut.WaitAsync(MockActivity.Object, CancellationToken.None);

        // Assert
        InvalidOperationException expectedException = new($"Failed to download asset '{AssetName}'.");
        waitTask.Should().BeFaultedWith(expectedException, within: Timeout);
        MockLogger.VerifyMessages(log =>
        {
            log.CloudAssetOrchestrator_Downloading(AssetName);
            log.CloudAssetOrchestrator_Failed(expectedException);
        });
    }

    [TestMethod]
    public void CloudAssetOrchestrator_ExecuteAsync_FaultsWhenDeserializationFails()
    {
        // Arrange
        InvalidOperationException parseException = new("Deserialization failed");
        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        MockAsset.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(parseException);
        CloudAssetOrchestrator sut = MakeSut();

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        Task waitTask = sut.WaitAsync(MockActivity.Object, CancellationToken.None);

        // Assert
        waitTask.Should().BeFaultedWith(parseException, within: Timeout);
        MockLogger.VerifyMessages(log =>
        {
            log.CloudAssetOrchestrator_Downloading(AssetName);
            log.CloudAssetOrchestrator_Failed(parseException);
        });
    }
}
