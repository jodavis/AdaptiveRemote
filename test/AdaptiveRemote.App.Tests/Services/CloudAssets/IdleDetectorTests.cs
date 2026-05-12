using System.Diagnostics;
using AdaptiveRemote.Services.IdleDetection;
using FluentAssertions;

namespace AdaptiveRemote.Services.CloudAssets;

[TestClass]
public class IdleDetectorTests
{
    private readonly MockOptions<CloudSettings> MockOptions = new();

    [TestMethod]
    public void WaitForIdleAsync_Waits_WhenNoScopedDetector()
    {
        // Arrange
        FakeUserActivityDetector fake = new(DateTime.MinValue);
        IdleDetector sut = new();
        IdleDetector.ScopedIdleDetector scoped = new([fake], sut, new FakeOptions(0));
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));

        // Act
        Task resultTask = sut.WaitForIdleAsync(cts.Token);

        // Assert
        resultTask.Should().NotBeComplete();
    }

    [TestMethod]
    public void WaitForIdleAsync_Completes_WhenScopedDetectorInitialized()
    {
        // Arrange
        FakeUserActivityDetector fake = new(DateTime.MinValue);
        IdleDetector sut = new();
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
        Task resultTask = sut.WaitForIdleAsync(cts.Token);

        IdleDetector.ScopedIdleDetector scoped = new([fake], sut, new FakeOptions(0));

        // Act
        scoped.InitializeAsync(null!, default);

        // Assert
        resultTask.Should().BeComplete();
    }

    [TestMethod]
    public void WaitForIdleAsync_Waits_WhenScopedHasBeenCleanedUp()
    {
        // Arrange
        FakeUserActivityDetector fake = new(DateTime.MinValue);
        IdleDetector sut = new();
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));

        IdleDetector.ScopedIdleDetector scoped = new([fake], sut, new FakeOptions(0));
        scoped.InitializeAsync(null!, default);

        // Act
        scoped.CleanUpAsync(null!, default);
        Task resultTask = sut.WaitForIdleAsync(cts.Token);

        // Assert
        resultTask.Should().NotBeComplete(because: "the scope is being recycled which should be considered non-idle");
    }

    [TestMethod]
    public void WaitForIdleAsync_Completes_WhenNoActivity()
    {
        // Arrange
        FakeUserActivityDetector fake = new(DateTime.MinValue);
        IdleDetector sut = new();
        IdleDetector.ScopedIdleDetector scoped = new([fake], sut, new FakeOptions(0));
        scoped.InitializeAsync(null!, default);
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));

        // Act
        Task resultTask = sut.WaitForIdleAsync(cts.Token);

        // Assert
        resultTask.Should().BeComplete();
    }

    [TestMethod]
    public void WaitForIdleAsync_WaitsForCooldown()
    {
        // Arrange
        DateTime now = DateTime.Now;
        FakeUserActivityDetector fake = new(now);
        int cooldown = 1; // seconds
        IdleDetector sut = new();
        IdleDetector.ScopedIdleDetector scoped = new([fake], sut, new FakeOptions(cooldown));
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        scoped.InitializeAsync(null!, default);
        Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        // Act
        Task waitTask = sut.WaitForIdleAsync(cts.Token);

        // Assert
        waitTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(cooldown + 1));
        sw.Elapsed.TotalSeconds.Should().BeGreaterThanOrEqualTo(cooldown);
    }

    [TestMethod]
    public void WaitForIdleAsync_Cancels_IfTokenCancelled()
    {
        // Arrange
        FakeUserActivityDetector fake = new(DateTime.Now);
        IdleDetector sut = new();
        IdleDetector.ScopedIdleDetector scoped = new([fake], sut, new FakeOptions(10));
        scoped.InitializeAsync(null!, default);
        CancellationTokenSource cts = new();
        Task task = sut.WaitForIdleAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        task.Should().BeCanceledWithin(TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public void WaitForIdleAsync_Throws_IfDetectorThrows()
    {
        // Arrange
        ThrowingUserActivityDetector fake = new();
        IdleDetector sut = new();
        IdleDetector.ScopedIdleDetector scoped = new([fake], sut, new FakeOptions(1));
        scoped.InitializeAsync(null!, default);
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));

        // Act
        Task resultTask = sut.WaitForIdleAsync(cts.Token);

        // Assert
        resultTask.Should().BeFaultedWith(new InvalidOperationException("Simulated error"));
    }

    [TestMethod]
    public void WaitForIdleAsync_Handles_EmptyDetectorList()
    {
        // Arrange
        IdleDetector sut = new();
        IdleDetector.ScopedIdleDetector scoped = new(new List<IUserActivityDetector>(), sut, new FakeOptions(0));
        scoped.InitializeAsync(null!, default);
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));

        // Act
        Task task = sut.WaitForIdleAsync(cts.Token);

        // Assert
        task.Should().BeComplete(because: "there are no activity detectors, so it is always idle");
    }

    // Helpers
    private class FakeUserActivityDetector : IUserActivityDetector
    {
        public DateTime LastActivityTime { get; set; }
        public FakeUserActivityDetector(DateTime t) => LastActivityTime = t;
    }

    private class ThrowingUserActivityDetector : IUserActivityDetector
    {
        public DateTime LastActivityTime => throw new InvalidOperationException("Simulated error");
    }

    private class FakeOptions : Microsoft.Extensions.Options.IOptions<CloudSettings>
    {
        public CloudSettings Value { get; }
        public FakeOptions(int cooldownSeconds) => Value = new CloudSettings { IdleCooldownSeconds = cooldownSeconds };
    }
}
