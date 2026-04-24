using FluentAssertions;

namespace AdaptiveRemote.Services.CloudAssets;

[TestClass]
public class FileSystemCloudAssetWatchServiceTests
{
    private static readonly TimeSpan WatchTimeout = TimeSpan.FromSeconds(5);

    private static FileSystemCloudAssetWatchService MakeSut(string stubFilePath)
        => new(new MockOptions<CloudSettings>(new CloudSettings { StubFilePath = stubFilePath }));

    [TestMethod]
    public async Task FileSystemCloudAssetWatchService_WaitForChangeAsync_UnblocksOnFileWriteAsync()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        try
        {
            FileSystemCloudAssetWatchService sut = MakeSut(tempFile);
            await sut.StartAsync(CancellationToken.None);

            Task waitTask = sut.WaitForChangeAsync(CancellationToken.None);

            // Act — write to the file after a short delay
            await Task.Delay(50);
            await File.WriteAllTextAsync(tempFile, "updated");

            // Assert
            waitTask.Should().BeCompleteWithin(WatchTimeout);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public async Task FileSystemCloudAssetWatchService_WaitForChangeAsync_UnblocksOnFileCreationAsync()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        string stubPath = Path.Combine(tempDir, "stub.json");

        try
        {
            FileSystemCloudAssetWatchService sut = MakeSut(stubPath);
            await sut.StartAsync(CancellationToken.None);

            Task waitTask = sut.WaitForChangeAsync(CancellationToken.None);

            // Act — create the file
            await Task.Delay(50);
            await File.WriteAllTextAsync(stubPath, "{}");

            // Assert
            waitTask.Should().BeCompleteWithin(WatchTimeout);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task FileSystemCloudAssetWatchService_WaitForChangeAsync_RapidEventsCollapseToOneNotificationAsync()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        try
        {
            FileSystemCloudAssetWatchService sut = MakeSut(tempFile);
            await sut.StartAsync(CancellationToken.None);

            // Act — trigger two rapid writes
            await Task.Delay(50);
            await File.WriteAllTextAsync(tempFile, "write1");
            await Task.Delay(10);
            await File.WriteAllTextAsync(tempFile, "write2");

            // First WaitForChangeAsync should complete
            Task firstWait = sut.WaitForChangeAsync(CancellationToken.None);
            firstWait.Should().BeCompleteWithin(WatchTimeout);

            // Second call should block because rapid events collapsed to one notification
            using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(200));
            Task secondWait = sut.WaitForChangeAsync(cts.Token);
            await secondWait.Awaiting(t => t).Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public async Task FileSystemCloudAssetWatchService_WaitForChangeAsync_RespectsCancellationAsync()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        try
        {
            FileSystemCloudAssetWatchService sut = MakeSut(tempFile);
            await sut.StartAsync(CancellationToken.None);

            using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(100));

            // Act
            Task waitTask = sut.WaitForChangeAsync(cts.Token);

            // Assert
            await waitTask.Awaiting(t => t).Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
