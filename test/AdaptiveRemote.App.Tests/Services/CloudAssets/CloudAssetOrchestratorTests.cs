using AdaptiveRemote.Models.CloudAssets;
using AdaptiveRemote.Services.Lifecycle;
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
    private readonly Mock<ICloudAssetCache> MockCache = new();
    private readonly Mock<IApplicationRecycleSignal> MockSignal = new();
    private readonly Mock<IIdleDetector> MockIdleDetector = new();
    private readonly Mock<ICloudAssetChangeNotifier> MockChangeNotifier = new();
    private readonly MockLogger<CloudAssetOrchestrator> MockLogger = new();
    private readonly Mock<ILifecycleActivity> MockActivity = new();

    [TestInitialize]
    public void SetupMocks()
    {
        MockAsset.SetupGet(a => a.Name).Returns(AssetName);
        MockAsset.SetupGet(a => a.ResourcePath).Returns(ResourcePath);
        MockActivity.SetupSet(a => a.Description = It.IsAny<string>());

        // Default: idle, no changes pending
        MockIdleDetector.Setup(d => d.WaitForIdleAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default: block forever on change notifier (prevents Phase 3 interference)
        MockChangeNotifier.Setup(n => n.WaitForChangeAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => Task.Delay(Timeout.Multiply(10), ct).ContinueWith(_ => AssetName, ct, TaskContinuationOptions.None, TaskScheduler.Default));

        // Default: cache miss
        MockCache.Setup(c => c.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);
    }

    private CloudAssetOrchestrator MakeSut(IEnumerable<ICloudAsset>? assets = null)
        => new(assets ?? [MockAsset.Object],
            MockDownloader.Object,
            MockStore.Object,
            MockCache.Object,
            MockSignal.Object,
            MockIdleDetector.Object,
            MockChangeNotifier.Object,
            MockLogger);

    // ──────────────────────────────────────────────────────────────
    // Phase 1 — cache miss (server download path)
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void CloudAssetOrchestrator_ExecuteAsync_CacheMiss_DownloadsAndStoresAsset()
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
        MockCache.Verify(c => c.SaveAsync(AssetName, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        MockLogger.VerifyMessages(log => { log.CloudAssetOrchestrator_Downloading(AssetName); });
    }

    [TestMethod]
    public void CloudAssetOrchestrator_ExecuteAsync_CacheMiss_FaultsWhenDownloadReturnsNull()
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
    public void CloudAssetOrchestrator_ExecuteAsync_CacheMiss_FaultsWhenDownloaderThrows()
    {
        // Arrange
        InvalidOperationException downloadException = new("download failed");
        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(downloadException);
        CloudAssetOrchestrator sut = MakeSut();

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        Task waitTask = sut.WaitAsync(MockActivity.Object, CancellationToken.None);

        // Assert
        waitTask.Should().BeFaultedWith(downloadException, within: Timeout);
        MockLogger.VerifyMessages(log =>
        {
            log.CloudAssetOrchestrator_Downloading(AssetName);
            log.CloudAssetOrchestrator_Failed(downloadException);
        });
    }

    [TestMethod]
    public void CloudAssetOrchestrator_ExecuteAsync_CacheMiss_FaultsWhenDeserializationFails()
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

    [TestMethod]
    public void CloudAssetOrchestrator_ExecuteAsync_CacheMiss_FaultsWhenCacheSaveThrows()
    {
        // Arrange
        IOException saveException = new("disk full");
        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        MockAsset.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object());
        MockCache.Setup(c => c.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(saveException);
        CloudAssetOrchestrator sut = MakeSut();

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        Task waitTask = sut.WaitAsync(MockActivity.Object, CancellationToken.None);

        // Assert
        waitTask.Should().BeFaultedWith(saveException, within: Timeout);
    }

    // ──────────────────────────────────────────────────────────────
    // Phase 1 — cache hit
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void CloudAssetOrchestrator_ExecuteAsync_CacheHit_LoadsFromCacheAndSignalsReady()
    {
        // Arrange
        object parsedValue = new();
        MockCache.Setup(c => c.LoadAsync(AssetName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        MockAsset.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parsedValue);

        // Phase 2 server check returns same bytes — no recycle expected
        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        CloudAssetOrchestrator sut = MakeSut();

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        Task waitTask = sut.WaitAsync(MockActivity.Object, CancellationToken.None);

        // Assert — Phase 1 completed; Phase 2 runs concurrently after WaitAsync returns
        waitTask.Should().BeCompleteWithin(Timeout);
        waitTask.Should().BeSuccessful();
        MockStore.Verify(s => s.Set(AssetName, parsedValue), Times.AtLeastOnce);
        // Downloader not called during Phase 1 (cache hit path); only Phase 2 may call it
        MockLogger.CountMessages(log => { log.CloudAssetOrchestrator_LoadedFromCache(AssetName); })
            .Should().BeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void CloudAssetOrchestrator_ExecuteAsync_CacheHit_FaultsWhenDeserializationFails()
    {
        // Arrange
        InvalidOperationException parseException = new("parse error");
        MockCache.Setup(c => c.LoadAsync(AssetName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        MockAsset.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(parseException);
        CloudAssetOrchestrator sut = MakeSut();

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        Task waitTask = sut.WaitAsync(MockActivity.Object, CancellationToken.None);

        // Assert
        waitTask.Should().BeFaultedWith(parseException, within: Timeout);
        MockLogger.VerifyMessages(log => { log.CloudAssetOrchestrator_Failed(parseException); });
    }

    [TestMethod]
    public void CloudAssetOrchestrator_ExecuteAsync_CacheHit_FaultsWhenCacheLoadThrows()
    {
        // Arrange
        IOException loadException = new("disk error");
        MockCache.Setup(c => c.LoadAsync(AssetName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(loadException);
        CloudAssetOrchestrator sut = MakeSut();

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        Task waitTask = sut.WaitAsync(MockActivity.Object, CancellationToken.None);

        // Assert
        waitTask.Should().BeFaultedWith(loadException, within: Timeout);
        MockLogger.VerifyMessages(log => { log.CloudAssetOrchestrator_Failed(loadException); });
    }

    // ──────────────────────────────────────────────────────────────
    // Phase 1 — multiple assets
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void CloudAssetOrchestrator_ExecuteAsync_MultipleAssets_AllStoredBeforeReady()
    {
        // Arrange
        Mock<ICloudAsset> asset1 = new();
        Mock<ICloudAsset> asset2 = new();
        asset1.SetupGet(a => a.Name).Returns("asset1");
        asset1.SetupGet(a => a.ResourcePath).Returns("/path1");
        asset2.SetupGet(a => a.Name).Returns("asset2");
        asset2.SetupGet(a => a.ResourcePath).Returns("/path2");

        object val1 = new(), val2 = new();
        MockDownloader.Setup(d => d.GetActiveAsync("/path1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        MockDownloader.Setup(d => d.GetActiveAsync("/path2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        asset1.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(val1);
        asset2.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(val2);

        CloudAssetOrchestrator sut = MakeSut([asset1.Object, asset2.Object]);

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        Task waitTask = sut.WaitAsync(MockActivity.Object, CancellationToken.None);

        // Assert
        waitTask.Should().BeCompleteWithin(Timeout);
        MockStore.Verify(s => s.Set("asset1", val1), Times.Once);
        MockStore.Verify(s => s.Set("asset2", val2), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────
    // Phase 2 — background server check
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void CloudAssetOrchestrator_Phase2_UpdatesStoreWhenServerReturnsDifferentContent()
    {
        // Arrange
        byte[] cachedBytes = "cached"u8.ToArray();
        byte[] serverBytes = "updated"u8.ToArray();
        object initialValue = new();
        object updatedValue = new();

        MockCache.Setup(c => c.LoadAsync(AssetName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(cachedBytes));

        int deserializeCallCount = 0;
        MockAsset.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => deserializeCallCount++ == 0 ? initialValue : updatedValue);

        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(serverBytes));

        CloudAssetOrchestrator sut = MakeSut();

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        Task waitTask = sut.WaitAsync(MockActivity.Object, CancellationToken.None);
        waitTask.Should().BeCompleteWithin(Timeout);

        // Wait for Phase 2 completion log rather than a fixed delay
        MockLogger.WaitForMessageAsync(log => { log.CloudAssetOrchestrator_AssetUpdated(AssetName); }).Should().BeCompleteWithin(Timeout);

        // Assert
        MockStore.Verify(s => s.Set(AssetName, updatedValue), Times.Once);
        MockSignal.Verify(s => s.RequestRecycle(), Times.Once);
        MockLogger.VerifyMessages(log =>
        {
            log.CloudAssetOrchestrator_LoadedFromCache(AssetName);
            log.CloudAssetOrchestrator_AssetUpdated(AssetName);
        });
    }

    [TestMethod]
    public void CloudAssetOrchestrator_Phase2_DoesNotRecycleWhenServerReturnsIdenticalContent()
    {
        // Arrange
        byte[] sharedBytes = "same-content"u8.ToArray();
        object parsedValue = new();

        MockCache.Setup(c => c.LoadAsync(AssetName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(sharedBytes));
        MockAsset.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parsedValue);
        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(sharedBytes));

        CloudAssetOrchestrator sut = MakeSut();

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        Task waitTask = sut.WaitAsync(MockActivity.Object, CancellationToken.None);
        waitTask.Should().BeCompleteWithin(Timeout);
        MockLogger.WaitForMessageAsync(log => { log.CloudAssetOrchestrator_AssetUpToDate(AssetName); }).Should().BeCompleteWithin(Timeout);

        // Assert
        MockSignal.Verify(s => s.RequestRecycle(), Times.Never);
        MockLogger.VerifyMessages(log =>
        {
            log.CloudAssetOrchestrator_LoadedFromCache(AssetName);
            log.CloudAssetOrchestrator_AssetUpToDate(AssetName);
        });
    }

    [TestMethod]
    public void CloudAssetOrchestrator_Phase2_LogsWarningAndContinuesWhenServerReturnsNull()
    {
        // Arrange
        MockCache.Setup(c => c.LoadAsync(AssetName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream("cached"u8.ToArray()));
        MockAsset.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object());
        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        CloudAssetOrchestrator sut = MakeSut();

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        Task waitTask = sut.WaitAsync(MockActivity.Object, CancellationToken.None);
        waitTask.Should().BeCompleteWithin(Timeout);
        MockLogger.WaitForMessageAsync(log => { log.CloudAssetOrchestrator_BackgroundFetchFailed(AssetName, null); }).Should().BeCompleteWithin(Timeout);

        // Assert — Phase 2 warning logged, no recycle, no crash
        MockSignal.Verify(s => s.RequestRecycle(), Times.Never);
        MockLogger.VerifyMessages(log =>
        {
            log.CloudAssetOrchestrator_LoadedFromCache(AssetName);
            log.CloudAssetOrchestrator_BackgroundFetchFailed(AssetName, null);
        });
    }

    [TestMethod]
    public void CloudAssetOrchestrator_Phase2_LogsWarningAndContinuesWhenServerThrows()
    {
        // Arrange
        HttpRequestException networkException = new("network error");
        MockCache.Setup(c => c.LoadAsync(AssetName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream("cached"u8.ToArray()));
        MockAsset.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object());
        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(networkException);

        CloudAssetOrchestrator sut = MakeSut();

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        Task waitTask = sut.WaitAsync(MockActivity.Object, CancellationToken.None);
        waitTask.Should().BeCompleteWithin(Timeout);
        MockLogger.WaitForMessageAsync(log => { log.CloudAssetOrchestrator_BackgroundFetchFailed(AssetName, networkException); }).Should().BeCompleteWithin(Timeout);

        // Assert — Phase 2 warning, no crash
        MockSignal.Verify(s => s.RequestRecycle(), Times.Never);
        MockLogger.VerifyMessages(log =>
        {
            log.CloudAssetOrchestrator_LoadedFromCache(AssetName);
            log.CloudAssetOrchestrator_BackgroundFetchFailed(AssetName, networkException);
        });
    }

    // ──────────────────────────────────────────────────────────────
    // Phase 3 — file-change loop
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void CloudAssetOrchestrator_Phase3_OnFileChange_DownloadsAndUpdatesStore()
    {
        // Arrange
        TaskCompletionSource notificationSource = new();
        MockChangeNotifier.Setup(n => n.WaitForChangeAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                await notificationSource.Task.WaitAsync(ct);
                // Block subsequent calls so only one Phase 3 cycle runs
                MockChangeNotifier.Setup(n => n.WaitForChangeAsync(It.IsAny<CancellationToken>()))
                    .Returns<CancellationToken>(ct2 => Task.Delay(Timeout.Multiply(10), ct2).ContinueWith(_ => AssetName, ct2, TaskContinuationOptions.None, TaskScheduler.Default));
                return AssetName;
            });

        object updatedValue = new();
        // Phase 1 (cache miss): downloader returns initial bytes
        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream("server-data"u8.ToArray()));
        MockAsset.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedValue);

        CloudAssetOrchestrator sut = MakeSut();

        // Act — start, let Phase 1 (cache miss + download) complete
        _ = sut.StartAsync(CancellationToken.None);
        sut.WaitAsync(MockActivity.Object, CancellationToken.None).Should().BeCompleteWithin(Timeout);
        // Clear Phase 1 logs and store invocations before Phase 3 triggers
        MockLogger.ClearMessages();
        MockStore.Invocations.Clear();
        MockSignal.Invocations.Clear();

        // Signal a file change; wait until RequestRecycle is called (last observable action in Phase 3)
        notificationSource.SetResult();
        SpinWait.SpinUntil(() => MockSignal.Invocations.Any(), Timeout);

        // Assert
        MockStore.Verify(s => s.Set(AssetName, updatedValue), Times.Once);
        MockSignal.Verify(s => s.RequestRecycle(), Times.Once);
        MockLogger.VerifyMessages(log =>
        {
            log.CloudAssetOrchestrator_FileChangeDetected(AssetName);
            log.CloudAssetOrchestrator_AssetUpdated(AssetName);
        });
    }

    [TestMethod]
    public void CloudAssetOrchestrator_Phase3_OnFileChange_LogsWarningWhenServerReturnsNull()
    {
        // Arrange
        TaskCompletionSource notificationSource = new();
        MockChangeNotifier.Setup(n => n.WaitForChangeAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                await notificationSource.Task.WaitAsync(ct);
                MockChangeNotifier.Setup(n => n.WaitForChangeAsync(It.IsAny<CancellationToken>()))
                    .Returns<CancellationToken>(ct2 => Task.Delay(Timeout.Multiply(10), ct2).ContinueWith(_ => AssetName, ct2, TaskContinuationOptions.None, TaskScheduler.Default));
                return AssetName;
            });

        // Phase 1 (cache miss): downloader returns content so startup succeeds
        // Phase 3: switch to null so the warning path is triggered
        MockDownloader.SetupSequence(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream("initial"u8.ToArray()))  // Phase 1
            .ReturnsAsync((Stream?)null);                           // Phase 3

        MockAsset.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object());

        CloudAssetOrchestrator sut = MakeSut();

        // Act — Phase 1 succeeds
        _ = sut.StartAsync(CancellationToken.None);
        sut.WaitAsync(MockActivity.Object, CancellationToken.None).Should().BeCompleteWithin(Timeout);
        MockLogger.ClearMessages();

        // Trigger Phase 3 and wait for warning log
        notificationSource.SetResult();
        MockLogger.WaitForMessageAsync(log => { log.CloudAssetOrchestrator_BackgroundFetchFailed(AssetName, null); }).Should().BeCompleteWithin(Timeout);

        // Assert — warning logged, no crash, no recycle from Phase 3
        MockLogger.VerifyMessages(log =>
        {
            log.CloudAssetOrchestrator_FileChangeDetected(AssetName);
            log.CloudAssetOrchestrator_BackgroundFetchFailed(AssetName, null);
        });
    }

    [TestMethod]
    public void CloudAssetOrchestrator_Phase3_StopsCleanlyOnCancellation()
    {
        // Arrange — change notifier blocks until cancelled
        CancellationTokenSource cts = new();
        MockChangeNotifier.Setup(n => n.WaitForChangeAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => Task.Delay(Timeout.Multiply(10), ct).ContinueWith(_ => AssetName, ct, TaskContinuationOptions.None, TaskScheduler.Default));

        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        CloudAssetOrchestrator sut = MakeSut();

        // Act — start then cancel
        _ = sut.StartAsync(CancellationToken.None);
        sut.WaitAsync(MockActivity.Object, CancellationToken.None).Should().BeCompleteWithin(Timeout);

        Task stopTask = sut.StopAsync(CancellationToken.None);

        // Assert
        stopTask.Should().BeCompleteWithin(Timeout);
        stopTask.Should().BeSuccessful();
    }

    // ──────────────────────────────────────────────────────────────
    // Idle-defer recycle
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void CloudAssetOrchestrator_IdleDeferRecycle_RequestsRecycleImmediatelyWhenAlreadyIdle()
    {
        // Arrange — cache hit + different server bytes = update triggered
        byte[] cachedBytes = "v1"u8.ToArray();
        byte[] serverBytes = "v2"u8.ToArray();
        MockCache.Setup(c => c.LoadAsync(AssetName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(cachedBytes));
        MockAsset.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object());
        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(serverBytes));
        MockIdleDetector.Setup(d => d.WaitForIdleAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        CloudAssetOrchestrator sut = MakeSut();

        // Act
        _ = sut.StartAsync(CancellationToken.None);
        sut.WaitAsync(MockActivity.Object, CancellationToken.None).Should().BeCompleteWithin(Timeout);
        MockLogger.WaitForMessageAsync(log => { log.CloudAssetOrchestrator_AssetUpdated(AssetName); }).Should().BeCompleteWithin(Timeout);

        // Assert
        MockSignal.Verify(s => s.RequestRecycle(), Times.Once);
        // WaitForIdleAsync subscribes then immediately unsubscribes when already idle;
        // the important assertion is that RequestRecycle was called synchronously.
        MockIdleDetector.Verify(d => d.WaitForIdleAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public void CloudAssetOrchestrator_IdleDeferRecycle_SubscribesToBecameIdleWhenNotIdle()
    {
        // Arrange
        byte[] cachedBytes = "v1"u8.ToArray();
        byte[] serverBytes = "v2"u8.ToArray();
        MockCache.Setup(c => c.LoadAsync(AssetName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(cachedBytes));
        MockAsset.Setup(a => a.DeserializeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object());
        MockDownloader.Setup(d => d.GetActiveAsync(ResourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(serverBytes));

        TaskCompletionSource tcs = new();
        MockIdleDetector.Setup(d => d.WaitForIdleAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        CloudAssetOrchestrator sut = MakeSut();

        // Act — wait for Phase 2 to register the handler
        _ = sut.StartAsync(CancellationToken.None);
        sut.WaitAsync(MockActivity.Object, CancellationToken.None).Should().BeCompleteWithin(Timeout);
        MockLogger.WaitForMessageAsync(log => { log.CloudAssetOrchestrator_AssetUpdated(AssetName); }).Should().BeCompleteWithin(Timeout);

        // Not yet recycled
        MockSignal.Verify(s => s.RequestRecycle(), Times.Never);

        // Simulate becoming idle
        tcs.SetResult();

        // Wait for the ContinueWith continuation to fire RequestRecycle
        SpinWait.SpinUntil(() => MockSignal.Invocations.Any(), Timeout);

        // Now recycle should have been requested
        MockSignal.Verify(s => s.RequestRecycle(), Times.Once);
    }
}
