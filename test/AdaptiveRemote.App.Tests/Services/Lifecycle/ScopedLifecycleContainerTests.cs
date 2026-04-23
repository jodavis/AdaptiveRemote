using FluentAssertions;
using Moq;

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
            .Callback<Exception>(ex => Assert.Fail(string.Format("SetFatalError was called on the activity: {0}", ex)));

        MockLogger.OutputWriter = TestContext;
    }

    [TestMethod]
    public async Task Initialize_AllServices_SucceedsAsync()
    {
        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        ScopedLifecycleContainer sut = CreateSut();

        await sut.InitializeAllAsync(default);

        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
        });

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

        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
        });

        t.Should().NotBeComplete();
        LatestLifecyclePhase.Should().Be(LifecyclePhase.SettingUp);
    }

    [TestMethod]
    public async Task Initialize_ImmediateFailure_ReportsFailureAsync()
    {
        Exception expected = new InvalidOperationException("fail1");

        Expect_InitializeAsyncOn(MockService1, Task.CompletedTask);
        Expect_InitializeAsyncOn(MockService2, Task.FromException(expected));
        // Service3 should not be started when immediate failure occurs

        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);

        Expect_LifecycleActivity_SetFatalError(expected);

        ScopedLifecycleContainer sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InitializeAllAsync(default));

        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_InitializingFailed(MockService2.Object.Name, expected);
        });

        LatestLifecyclePhase.Should().Be(LifecyclePhase.SettingUp);
    }

    [TestMethod]
    public async Task Initialize_DelayedFailure_ReportsFailureAsync()
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

        await Assert.ThrowsAsync<InvalidOperationException>(() => initTask);

        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_InitializingFailed(MockService2.Object.Name, expected);
        });

        LatestLifecyclePhase.Should().Be(LifecyclePhase.SettingUp);
    }

    [TestMethod]
    public async Task Initialize_ImmediateFailure_CancelsPendingAsync()
    {
        Exception expected = new InvalidOperationException("fail1");

        Expect_InitializeAsyncOn(MockService1, IncompleteTask);
        Expect_InitializeAsyncOn(MockService2, Task.FromException(expected));
        // Service3 should not be started when immediate failure occurs

        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);

        Expect_LifecycleActivity_SetFatalError(expected);

        ScopedLifecycleContainer sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InitializeAllAsync(default));

        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_InitializingFailed(MockService2.Object.Name, expected);
        });

        LatestLifecyclePhase.Should().Be(LifecyclePhase.SettingUp);
    }

    [TestMethod]
    public void Cleanup_BlocksUntilAllComplete()
    {
        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        Expect_CleanupAsyncOn(MockService1, result: IncompleteTask);
        Expect_CleanupAsyncOn(MockService2, result: IncompleteTask);
        Expect_CleanupAsyncOn(MockService3, result: IncompleteTask);

        ScopedLifecycleContainer sut = CreateSut();

        sut.InitializeAllAsync(default)
            .Should().BeComplete(because: "all services initialize without errors");

        Task cleanupTask = sut.CleanUpInitializedServicesAsync(default);

        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService3.Object.Name);
        });

        cleanupTask.Should().NotBeComplete();
        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp);
    }

    [TestMethod]
    public async Task Cleanup_ReportsErrorsAsync()
    {
        Exception expected1 = new InvalidOperationException("Error 1");
        Exception expected2 = new FormatException("Error 2");

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        Expect_CleanupAsyncOn(MockService1, result: Task.FromException(expected1));
        Expect_CleanupAsyncOn(MockService2, result: Task.FromException(expected2));
        Expect_CleanupAsyncOn(MockService3);

        Expect_LifecycleActivity_SetFatalError(expected1, expected2);

        ScopedLifecycleContainer sut = CreateSut();

        sut.InitializeAllAsync(default)
            .Should().BeComplete(because: "all services initialize without errors");

        await sut.CleanUpInitializedServicesAsync(default);

        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUpFailed(MockService1.Object.Name, expected1);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleaningUpFailed(MockService2.Object.Name, expected2);
            log.ApplicationLifecycle_CleaningUp(MockService3.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService3.Object.Name);
        });

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
                    string.Format("Unexpected exception for SetFatalError: {0}", ex));
            })
            .Verifiable(Times.Exactly(expectedExceptions.Length));

}
