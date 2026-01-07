#pragma warning disable IDE0005 // False positive: MockLogger type requires AdaptiveRemote.TestUtilities
using AdaptiveRemote.Logging;
using AdaptiveRemote.TestUtilities;
using FluentAssertions;
using Moq;
#pragma warning restore IDE0005

namespace AdaptiveRemote.Services.Lifecycle;

[TestClass]
public class ScopedLifecycleContainerTests
{
    private static readonly Task IncompleteTask = new TaskCompletionSource().Task;

    private readonly Mock<IScopedLifecycle> MockService1 = new();
    private readonly Mock<IScopedLifecycle> MockService2 = new();
    private readonly Mock<IScopedLifecycle> MockService3 = new();

    private readonly Mock<ILifecycleViewController> MockLifecycleViewController = new();
    private readonly Mock<ILifecycleActivity> MockActivity = new();
    private readonly MockLogger<ScopedLifecycleContainer> MockLogger = new();

    public TestContext? TestContext { get; set; }
    public LifecyclePhase LatestLifecyclePhase { get; private set; }

    [TestInitialize]
    public void Setup()
    {
        MockService1.SetupGet(x => x.Name).Returns(nameof(MockService1));
        MockService2.SetupGet(x => x.Name).Returns(nameof(MockService2));
        MockService3.SetupGet(x => x.Name).Returns(nameof(MockService3));

        MockLifecycleViewController
            .Setup(x => x.StartTask(It.IsAny<string>()))
            .Callback<string>(desc => MockActivity.Name = desc)
            .Returns(MockActivity.Object);
        MockLifecycleViewController
            .Setup(x => x.SetPhase(It.IsAny<LifecyclePhase>()))
            .Callback<LifecyclePhase>(phase => LatestLifecyclePhase = phase);
        MockActivity
            .Setup(x => x.SetFatalError(It.IsAny<Exception>()))
            .Callback<Exception>(ex => Assert.Fail("SetFatalError was called on the activity: {0}", ex));

        MockLogger.OutputWriter = TestContext;
    }

    [TestMethod]
    public async Task Initialize_AllServices_Succeeds()
    {
        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        ScopedLifecycleContainer sut = CreateSut();

        await sut.InitializeAllAsync(default);

        MockLogger.VerifyMessages(
            Expect_InitializingMessage(MockService1),
            Expect_InitializedMessage(MockService1),
            Expect_InitializingMessage(MockService2),
            Expect_InitializedMessage(MockService2),
            Expect_InitializingMessage(MockService3),
            Expect_InitializedMessage(MockService3));

        LatestLifecyclePhase.Should().Be(LifecyclePhase.Ready);
    }

    [TestMethod]
    public void Initialize_SomeIncomplete_DoesNotSetReady()
    {
        Expect_InitializeAsyncOn(MockService1, IncompleteTask);
        Expect_InitializeAsyncOn(MockService2, IncompleteTask);
        Expect_InitializeAsyncOn(MockService3, IncompleteTask);

        ScopedLifecycleContainer sut = CreateSut();

        Task t = sut.InitializeAllAsync(default);

        MockLogger.VerifyMessages(
            Expect_InitializingMessage(MockService1),
            Expect_InitializingMessage(MockService2),
            Expect_InitializingMessage(MockService3));

        t.Should().NotBeComplete();
        LatestLifecyclePhase.Should().Be(LifecyclePhase.SettingUp);
    }

    [TestMethod]
    public async Task Initialize_ImmediateFailure_CleansUpAndReports()
    {
        Exception expected = new InvalidOperationException("fail1");

        Expect_InitializeAsyncOn(MockService1, Task.CompletedTask);
        Expect_InitializeAsyncOn(MockService2, Task.FromException(expected));
        // Service3 should not be started when immediate failure occurs

        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);

        Expect_LifecycleActivity_SetFatalError(expected);

        ScopedLifecycleContainer sut = CreateSut();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => sut.InitializeAllAsync(default));

        MockLogger.VerifyMessages(
            Expect_InitializingMessage(MockService1),
            Expect_InitializedMessage(MockService1),
            Expect_InitializingMessage(MockService2),
            Expect_InitializingFailedMessage(MockService2, expected),
            Expect_CleaningUpMessage(MockService1),
            Expect_CleanedUpMessage(MockService1),
            Expect_CleaningUpMessage(MockService2),
            Expect_CleanedUpMessage(MockService2));

        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp);
    }

    [TestMethod]
    public async Task Initialize_DelayedFailure_CleansUpAndReports()
    {
        Exception expected = new InvalidOperationException("fail1");
        TaskCompletionSource tcs = new TaskCompletionSource();

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2, tcs.Task);
        Expect_InitializeAsyncOn(MockService3);

        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);
        Expect_CleanupAsyncOn(MockService3);

        Expect_LifecycleActivity_SetFatalError(expected);

        ScopedLifecycleContainer sut = CreateSut();

        Task initTask = sut.InitializeAllAsync(default);

        tcs.SetException(expected);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => initTask);

        MockLogger.VerifyMessages(
            Expect_InitializingMessage(MockService1),
            Expect_InitializedMessage(MockService1),
            Expect_InitializingMessage(MockService2),
            Expect_InitializingMessage(MockService3),
            Expect_InitializedMessage(MockService3),
            Expect_InitializingFailedMessage(MockService2, expected),
            Expect_CleaningUpMessage(MockService1),
            Expect_CleanedUpMessage(MockService1),
            Expect_CleaningUpMessage(MockService2),
            Expect_CleanedUpMessage(MockService2),
            Expect_CleaningUpMessage(MockService3),
            Expect_CleanedUpMessage(MockService3));

        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp);
    }

    [TestMethod]
    public async Task Initialize_ImmediateFailure_CancelsPending()
    {
        Exception expected = new InvalidOperationException("fail1");

        Expect_InitializeAsyncOn(MockService1, IncompleteTask);
        Expect_InitializeAsyncOn(MockService2, Task.FromException(expected));
        // Service3 should not be started when immediate failure occurs

        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);

        Expect_LifecycleActivity_SetFatalError(expected);

        ScopedLifecycleContainer sut = CreateSut();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => sut.InitializeAllAsync(default));

        MockLogger.VerifyMessages(
            Expect_InitializingMessage(MockService1),
            Expect_InitializingMessage(MockService2),
            Expect_InitializingFailedMessage(MockService2, expected),
            Expect_CleaningUpMessage(MockService1),
            Expect_CleanedUpMessage(MockService1),
            Expect_CleaningUpMessage(MockService2),
            Expect_CleanedUpMessage(MockService2));

        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp);
    }

    [TestMethod]
    public void Cleanup_BlocksUntilAllComplete()
    {
        Expect_CleanupAsyncOn(MockService1, result: IncompleteTask);
        Expect_CleanupAsyncOn(MockService2, result: IncompleteTask);
        Expect_CleanupAsyncOn(MockService3, result: IncompleteTask);

        ScopedLifecycleContainer sut = CreateSut();

        Task cleanupTask = sut.CleanUpAllAsync(default);

        MockLogger.VerifyMessages(
            Expect_CleaningUpMessage(MockService1),
            Expect_CleaningUpMessage(MockService2),
            Expect_CleaningUpMessage(MockService3));

        cleanupTask.Should().NotBeComplete();
        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp);
    }

    [TestMethod]
    public async Task Cleanup_ReportsErrors()
    {
        Exception expected1 = new InvalidOperationException("Error 1");
        Exception expected2 = new FormatException("Error 2");

        Expect_CleanupAsyncOn(MockService1, result: Task.FromException(expected1));
        Expect_CleanupAsyncOn(MockService2, result: Task.FromException(expected2));
        Expect_CleanupAsyncOn(MockService3);

        Expect_LifecycleActivity_SetFatalError(expected1, expected2);

        ScopedLifecycleContainer sut = CreateSut();

        await sut.CleanUpAllAsync(default);

        MockLogger.VerifyMessages(
            Expect_CleaningUpMessage(MockService1),
            Expect_CleaningUpFailedMessage(MockService1, expected1),
            Expect_CleaningUpMessage(MockService2),
            Expect_CleaningUpFailedMessage(MockService2, expected2),
            Expect_CleaningUpMessage(MockService3),
            Expect_CleanedUpMessage(MockService3));

        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp);
    }

    [TestMethod]
    public async Task Initialize_Cancellation_TriggersCleanup()
    {
        using CancellationTokenSource cts = new CancellationTokenSource();

        Expect_InitializeAsyncOn(MockService1, IncompleteTask);
        Expect_InitializeAsyncOn(MockService2, IncompleteTask);
        Expect_InitializeAsyncOn(MockService3, IncompleteTask);

        // Expect cleanup for all services that may have started
        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);
        Expect_CleanupAsyncOn(MockService3);

        ScopedLifecycleContainer sut = CreateSut();

        Task initTask = sut.InitializeAllAsync(cts.Token);

        // Cancel to simulate shutdown
        cts.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => initTask);

        MockLogger.VerifyMessages(
            Expect_InitializingMessage(MockService1),
            Expect_InitializingMessage(MockService2),
            Expect_InitializingMessage(MockService3),
            Expect_CleaningUpMessage(MockService1),
            Expect_CleanedUpMessage(MockService1),
            Expect_CleaningUpMessage(MockService2),
            Expect_CleanedUpMessage(MockService2),
            Expect_CleaningUpMessage(MockService3),
            Expect_CleanedUpMessage(MockService3));

        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp);
    }

    private ScopedLifecycleContainer CreateSut()
        => new(new[] { MockService1.Object, MockService2.Object, MockService3.Object }, MockLifecycleViewController.Object, MockLogger);

    private static void Expect_InitializeAsyncOn(Mock<IScopedLifecycle> service, Task? result = default)
        => service
            .Setup(x => x.InitializeAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(result)
            .Verifiable(Times.Once);

    private static void Expect_CleanupAsyncOn(Mock<IScopedLifecycle> service, Task? result = default)
        => service
            .Setup(x => x.CleanUpAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(result)
            .Verifiable(Times.Once);

    private void Expect_LifecycleActivity_SetFatalError(params Exception[] expectedExceptions)
        => MockActivity
            .Setup(x => x.SetFatalError(It.IsAny<Exception>()))
            .Callback<Exception>(ex =>
            {
                Assert.IsTrue(expectedExceptions.Any(x => $"{x.GetType().FullName};{x.Message}" == $"{ex.GetType().FullName};{ex.Message}"),
                    "Unexpected exception for SetFatalError: {0}", ex);
            })
            .Verifiable(Times.Exactly(expectedExceptions.Length));

    private static string Expect_InitializingMessage(Mock<IScopedLifecycle> service)
        => $"Information[701]: {string.Format(LoggingMessages.ApplicationLifecycle_Initializing, service.Object.Name)}";
    private static string Expect_InitializedMessage(Mock<IScopedLifecycle> service)
        => $"Information[702]: {string.Format(LoggingMessages.ApplicationLifecycle_Initialized, service.Object.Name)}";
    private static string Expect_InitializingFailedMessage(Mock<IScopedLifecycle> service, Exception error)
        => $"Error[703]: {string.Format(LoggingMessages.ApplicationLifecycle_InitializingFailed, service.Object.Name, $"{error.GetType().FullName}: {error.Message}")}";
    private static string Expect_CleaningUpMessage(Mock<IScopedLifecycle> service)
        => $"Information[704]: {string.Format(LoggingMessages.ApplicationLifecycle_CleaningUp, service.Object.Name)}";
    private static string Expect_CleanedUpMessage(Mock<IScopedLifecycle> service)
        => $"Information[705]: {string.Format(LoggingMessages.ApplicationLifecycle_CleanedUp, service.Object.Name)}";
    private static string Expect_CleaningUpFailedMessage(Mock<IScopedLifecycle> service, Exception error)
        => $"Error[706]: {string.Format(LoggingMessages.ApplicationLifecycle_CleaningUpFailed, service.Object.Name, $"{error.GetType().FullName}: {error.Message}")}";
}
