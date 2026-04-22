using AdaptiveRemote.Models;
using FluentAssertions;
using Moq;

namespace AdaptiveRemote.Services.Lifecycle;

[TestClass]
public class ApplicationLifecycleTests
{
    private static readonly Task IncompleteTask = new TaskCompletionSource().Task;

    private readonly Mock<IScopedLifecycle> MockService1 = new();
    private readonly Mock<IScopedLifecycle> MockService2 = new();
    private readonly Mock<IScopedLifecycle> MockService3 = new();

    private readonly Mock<IApplicationScopeProvider> MockScopeProvider = new();
    private readonly Mock<ILifecycleViewController> MockLifecycleViewController = new();
    private readonly Mock<ILifecycleActivity> MockActivity = new();
    private readonly Mock<IServiceProvider> MockServiceProvider = new();
    private readonly Mock<IApplicationRecycleSignal> MockSignal = new();
    private readonly MockLogger<ApplicationLifecycle, ScopedLifecycleContainer> MockLogger = new();

    public TestContext? TestContext { get; set; }

    public LifecyclePhase LatestLifecyclePhase { get; private set; }

    private ApplicationLifecycle CreateSut(params Mock<IPreScopeInitializer>[] preScopeInitializers) 
        => CreateSutWithSignal(MockSignal.Object, preScopeInitializers);

    private ApplicationLifecycle CreateSutWithSignal(IApplicationRecycleSignal signal, params Mock<IPreScopeInitializer>[] preInitializers)
        => new ApplicationLifecycle(
            MockScopeProvider.Object,
            MockLifecycleViewController.Object,
            signal,
            preInitializers.Select(x => x.Object),
            MockLogger);

    private static Mock<IPreScopeInitializer> CreatePreScopeInitializer(string name, Task? result = null)
    {
        Mock<IPreScopeInitializer> mockPreInit = new();
        mockPreInit
            .SetupGet(x => x.Name)
            .Returns(name);
        mockPreInit
            .Setup(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(result ?? Task.CompletedTask)
            .Verifiable(Times.Once);
        return mockPreInit;
    }

    [TestInitialize]
    public void SetupMocks()
    {
        MockScopeProvider
            .Setup(x => x.InvokeInScopeAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(delegate (Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken)
            {
                return workItem.Invoke(MockServiceProvider.Object, cancellationToken);
            });

        MockService1
            .SetupGet(x => x.Name)
            .Returns(nameof(MockService1));
        MockService2
            .SetupGet(x => x.Name)
            .Returns(nameof(MockService2));
        MockService3
            .SetupGet(x => x.Name)
            .Returns(nameof(MockService3));

        MockService1
            .Setup(x => x.InitializeAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        MockService2
            .Setup(x => x.InitializeAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        MockService3
            .Setup(x => x.InitializeAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);

        MockService1
            .Setup(x => x.CleanUpAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        MockService2
            .Setup(x => x.CleanUpAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        MockService3
            .Setup(x => x.CleanUpAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);

        MockLifecycleViewController
            .Setup(x => x.StartTask(It.IsAny<string>()))
            .Callback(delegate (string description) { MockActivity.Name = description; })
            .Returns(MockActivity.Object);
        MockLifecycleViewController
            .Setup(x => x.SetPhase(It.IsAny<LifecyclePhase>()))
            .Callback(delegate (LifecyclePhase phase) { LatestLifecyclePhase = phase; });
        MockActivity
            .Setup(x => x.SetFatalError(It.IsAny<Exception>()))
            .Callback(delegate (Exception ex) { Assert.Fail("SetFatalError was called on the activity: {0}", ex); });

        MockSignal
            .SetupGet(x => x.Token)
            .Returns(CancellationToken.None); // Never fires; existing tests don't exercise recycle

        MockScopeProvider
            .Setup(x => x.RecycleScopeAsync())
            .Throws(() => new AssertFailedException($"Unexpected call to {nameof(IApplicationScopeProvider.RecycleScopeAsync)}"))
            .Verifiable(Times.Never);

        MockLogger.OutputWriter = TestContext;
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        Verify(MockServiceProvider, nameof(MockServiceProvider));
        Verify(MockScopeProvider, nameof(MockScopeProvider));
        Verify(MockSignal, nameof(MockSignal));

        Verify(MockService1, nameof(MockService1));
        Verify(MockService2, nameof(MockService2));
        Verify(MockService3, nameof(MockService3));

        Verify(MockLifecycleViewController, nameof(MockLifecycleViewController));
        Verify(MockActivity, nameof(MockActivity));

        static void Verify(Mock mock, string name)
        {
            try
            {
                mock.Verify();
            }
            catch (MockException e)
            {
                throw new AssertFailedException($"Verify failed on {name}: {e.Message}");
            }
        }
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_StartsExecuteTaskAndInitializesScopedLifecycleServices()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        // Act
        Task startTask = sut.StartAsync(default);

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_ScopeReady();
        });

        startTask.Should().BeComplete(because: "StartAsync should complete after all services are initialized");
        sut.ExecuteTask.Should().NotBeComplete(because: "ExecuteTask should remain running after startup");
        Assert.AreEqual(LifecyclePhase.Ready, LatestLifecyclePhase, nameof(LatestLifecyclePhase));
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_InitializesAllServicesWhileSomeComplete()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1, IncompleteTask);
        Expect_InitializeAsyncOn(MockService2, IncompleteTask);
        Expect_InitializeAsyncOn(MockService3, IncompleteTask);

        // Act
        Task startTask = sut.StartAsync(default);

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
        });

        startTask.Should().BeComplete(because: "StartAsync should complete after all services are initialized");
        sut.ExecuteTask.Should().NotBeComplete(because: "ExecuteTask should remain running after startup");
        LatestLifecyclePhase.Should().Be(LifecyclePhase.SettingUp, because: "Services are still initializing");
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_ImmediateFailure_LogsErrorAndDoesNotStartOtherServicesAndCleansUpServices()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Exception expectedError1 = new InvalidOperationException("Error 1");

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1, Task.CompletedTask);
        Expect_InitializeAsyncOn(MockService2, Task.FromException(expectedError1));

        Expect_SetFatalErrorOn(MockActivity, expectedError1);

        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);

        // Act
        Task startTask = sut.StartAsync(default);

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_InitializingFailed(MockService2.Object.Name, expectedError1);
            log.ApplicationLifecycle_ShuttingDown();
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService2.Object.Name);
        });

        startTask.Should().BeComplete(because: "StartAsync should complete after all services are initialized");
        sut.ExecuteTask.Should().BeComplete(because: "ExecuteTask should exit if the loop ends");
        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp, because: "Services are being cleaned up after failure");
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_DelayedFailure_LogsErrorAndDoesNotStartOtherServicesAndCleansUpServices()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Exception expectedError1 = new InvalidOperationException("Error 1");
        TaskCompletionSource tcs = new();

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2, tcs.Task);
        Expect_InitializeAsyncOn(MockService3);

        Expect_SetFatalErrorOn(MockActivity, expectedError1);

        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);
        Expect_CleanupAsyncOn(MockService3);

        Task startTask = sut.StartAsync(default);

        // In .NET 10, StartAsync returns immediately, so we need to wait for services to start initializing
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "StartAsync should complete quickly");
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_Initializing(MockService3.Object.Name), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "Services should start initializing");

        // Act
        tcs.SetException(expectedError1);

        // Wait for cleanup to complete
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_CleanedUp(MockService3.Object.Name), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "Cleanup should complete after error");

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_InitializingFailed(MockService2.Object.Name, expectedError1);
            log.ApplicationLifecycle_ShuttingDown();
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService3.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService3.Object.Name);
        });

        sut.ExecuteTask.Should().BeComplete(because: "ExecuteTask should exit if the loop ends");
        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp, because: "Services are being cleaned up after failure");
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_ErrorDuringConstructor_SetsFatalError()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Exception expectedError1 = new InvalidOperationException("Error 1");
        MockServiceProvider
            .Setup(x => x.GetService(typeof(ScopedLifecycleContainer)))
            .Throws(expectedError1)
            .Verifiable(Times.Once);

        Expect_SetFatalErrorOn(MockLifecycleViewController, expectedError1);

        // Act
        Task startTask = sut.StartAsync(default);

        // Assert
        // In .NET 10, StartAsync returns immediately
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "StartAsync completes quickly");
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeConstructionFailed(expectedError1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "Error should be logged");
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_ScopeConstructionFailed(expectedError1);
            log.ApplicationLifecycle_ShuttingDown();
        });
    }

    [TestMethod]
    public void ApplicationLifecycle_StopAsync_AfterErrorDuringConstructor_DoesNothing()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Exception expectedError1 = new InvalidOperationException("Error 1");
        MockServiceProvider
            .Setup(x => x.GetService(typeof(ScopedLifecycleContainer)))
            .Throws(expectedError1)
            .Verifiable(Times.Once);

        Expect_SetFatalErrorOn(MockLifecycleViewController, expectedError1);

        Task startTask = sut.StartAsync(default);

        // In .NET 10, StartAsync returns immediately, wait for error to be logged
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "StartAsync completes quickly");
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeConstructionFailed(expectedError1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "Error should be logged");

        // Act
        Task stopTask = sut.StopAsync(default);

        // Assert
        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "StopAsync should have nothing to do");
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_ScopeConstructionFailed(expectedError1);
            log.ApplicationLifecycle_ShuttingDown();
        });
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_ImmediateFailure_CancelsStartupThatIsInProgress()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Exception expectedError1 = new InvalidOperationException("Error 1");

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1, IncompleteTask);
        Expect_InitializeAsyncOn(MockService2, Task.FromException(expectedError1));

        Expect_SetFatalErrorOn(MockActivity, expectedError1);

        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);

        // Act
        Task startTask = sut.StartAsync(default);

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_InitializingFailed(MockService2.Object.Name, expectedError1);
            log.ApplicationLifecycle_ShuttingDown();
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService2.Object.Name);
        });

        startTask.Should().BeComplete(because: "StartAsync should complete after all services are initialized");
        sut.ExecuteTask.Should().BeComplete(because: "ExecuteTask should exit if the loop ends");
        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp, because: "Services are being cleaned up after failure");
    }

    [TestMethod]
    public void ApplicationLifecycle_StopAsync_DisposesScopeAndCompletesTask()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);
        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);
        Expect_CleanupAsyncOn(MockService3);

        Task startTask = sut.StartAsync(default);

        // In .NET 10, StartAsync returns immediately, so we need to wait for initialization to complete
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "StartAsync should complete quickly");
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_Initialized(MockService3.Object.Name), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "All services should initialize");

        // Act
        Task stopTask = sut.StopAsync(default);

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_ScopeReady();
            log.ApplicationLifecycle_ShuttingDown();
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService3.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService3.Object.Name);
        });

        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "StopAsync should complete after all services are cleaned up");
        sut.ExecuteTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "ExecuteTask should complete after all services have stopped");
        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp, because: "we stay in this state after services are stopped, until the application exits");
    }

    [TestMethod]
    public void ApplicationLifecycle_StopAsync_BlocksUntilServicesAreCleanedUp()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);
        Expect_CleanupAsyncOn(MockService1, result: IncompleteTask);
        Expect_CleanupAsyncOn(MockService2, result: IncompleteTask);
        Expect_CleanupAsyncOn(MockService3, result: IncompleteTask);

        Task startTask = sut.StartAsync(default);

        // In .NET 10, StartAsync returns immediately, so we need to wait for initialization to complete
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "StartAsync should complete quickly");
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_Initialized(MockService3.Object.Name), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "All services should initialize");

        // Act
        Task stopTask = sut.StopAsync(default);

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_ScopeReady();
            log.ApplicationLifecycle_ShuttingDown();
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService3.Object.Name);
        });

        stopTask.Should().NotBeComplete(because: "StopAsync should block until all services are cleaned up");
        sut.ExecuteTask.Should().NotBeComplete(because: "ExecuteTask should remain running after startup");
        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp, because: "Services are being cleaned up for StopAsync");
    }

    [TestMethod]
    public void ApplicationLifecycle_StopAsync_ReportsErrorsInCleanUp()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Exception expectedError1 = new InvalidOperationException("Error 1");
        Exception expectedError2 = new FormatException("Error 2");

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);
        Expect_CleanupAsyncOn(MockService1, result: Task.FromException(expectedError1));
        Expect_CleanupAsyncOn(MockService2, result: Task.FromException(expectedError2));
        Expect_CleanupAsyncOn(MockService3);
        Expect_SetFatalErrorOn(MockActivity, expectedError1, expectedError2);

        Task startTask = sut.StartAsync(default);

        // In .NET 10, StartAsync returns immediately, so we need to wait for initialization to complete
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "StartAsync should complete quickly");
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_Initialized(MockService3.Object.Name), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "All services should initialize");

        // Act
        Task stopTask = sut.StopAsync(default);

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_ScopeReady();
            log.ApplicationLifecycle_ShuttingDown();
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUpFailed(MockService1.Object.Name, expectedError1);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleaningUpFailed(MockService2.Object.Name, expectedError2);
            log.ApplicationLifecycle_CleaningUp(MockService3.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService3.Object.Name);
        });

        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "StopAsyc should complete after all services are cleaned up");
        sut.ExecuteTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "ExecuteTask should complete after all services have stopped");
        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp, because: "services are being cleaned up for StopAsync, even though there was an error");
    }

    [TestMethod]
    public void ApplicationLifecycle_StopAsync_CancelsInitializeMethodsThatAreWaiting()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2, result: IncompleteTask);
        Expect_InitializeAsyncOn(MockService3);
        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);
        Expect_CleanupAsyncOn(MockService3);

        Task startTask = sut.StartAsync(default);

        // In .NET 10, StartAsync returns immediately, so we need to wait for initialization to start
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "StartAsync should complete quickly");
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_Initializing(MockService2.Object.Name), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "MockService2 should start initializing");

        // Act
        Task stopTask = sut.StopAsync(default);

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_ShuttingDown();
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService3.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService3.Object.Name);
        });

        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "StopAsync should complete after all services are cleaned up");
        sut.ExecuteTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "ExecuteTask should complete after all services have stoped");
        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp, because: "we stay in this state after StopAsync until the application exits, even after services have cleaned up");
    }

    [TestMethod]
    public void ApplicationLifecycle_StopAsync_AfterInitializeFailure_DoesNothing()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Exception expectedError1 = new InvalidOperationException("Error 1");

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1, Task.CompletedTask);
        Expect_InitializeAsyncOn(MockService2, Task.FromException(expectedError1));

        Expect_SetFatalErrorOn(MockActivity, expectedError1);

        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);

        Task startTask = sut.StartAsync(default);

        // In .NET 10, StartAsync returns immediately, so we need to wait for initialization to complete
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "StartAsync should complete quickly");
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_CleanedUp(MockService2.Object.Name), TimeSpan.FromSeconds(1)).Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "Cleanup should complete after initialization failure");

        // Act
        Task stopTask = sut.StopAsync(default);

        // Assert
        stopTask.Should().BeCompleteWithin(TimeSpan.FromMilliseconds(100),
            because: "StopAsync should complete after all services are cleaned up, even if some have failed");

        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_InitializingFailed(MockService2.Object.Name, expectedError1);
            log.ApplicationLifecycle_ShuttingDown();
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService2.Object.Name);
        });

        sut.ExecuteTask.Should().BeComplete(because: "ExecuteTask should complete after all services have stopped");
        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp, because: "we stay in this state until the application exits");
    }

    private static void Expect_InitializeAsyncOn(Mock<IScopedLifecycle> service, Task? result = default)
        => service
            .Setup(x => x.InitializeAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(result)
            .Verifiable(Times.Once);

    private static void Expect_InitializeAsyncAtLeastOnce(Mock<IScopedLifecycle> service, Task? result = default)
        => service
            .Setup(x => x.InitializeAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(result)
            .Verifiable(Times.AtLeastOnce);

    private static void Expect_CleanupAsyncOn(Mock<IScopedLifecycle> service, Task? result = default)
        => service
            .Setup(x => x.CleanUpAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(result)
            .Verifiable(Times.Once);

    private static void Expect_CleanupAsyncAtLeastOnce(Mock<IScopedLifecycle> service, Task? result = default)
        => service
            .Setup(x => x.CleanUpAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(result)
            .Verifiable(Times.AtLeastOnce);

    private static void Expect_SetFatalErrorOn(Mock<ILifecycleActivity> activity, params Exception[] expectedExceptions)
        => activity
            .Setup(x => x.SetFatalError(It.IsAny<Exception>()))
            .Callback(delegate (Exception ex)
            {
                Assert.IsTrue(expectedExceptions.Any(x => $"{x.GetType().FullName};{x.Message}" == $"{ex.GetType().FullName};{ex.Message}"),
                    "Unexpected exception for SetFatalError: {0}", ex);
            })
            .Verifiable(Times.Exactly(expectedExceptions.Length));

    private static void Expect_SetFatalErrorOn(Mock<ILifecycleViewController> controller, params Exception[] expectedExceptions)
        => controller
            .Setup(x => x.SetFatalError(It.IsAny<Exception>()))
            .Callback(delegate (Exception ex)
            {
                Assert.IsTrue(expectedExceptions.Any(x => $"{x.GetType().FullName};{x.Message}" == $"{ex.GetType().FullName};{ex.Message}"),
                    "Unexpected exception for SetFatalError: {0}", ex);
            })
            .Verifiable(Times.Exactly(expectedExceptions.Length));

    private static void Expect_RecycleScopeAsyncOn(Mock<IApplicationScopeProvider> provider, Times? times = null) 
        => provider
            .Setup(x => x.RecycleScopeAsync())
            .WithStandardTaskBehavior()
            .Verifiable(times ?? Times.Once());

    private void Expect_GetServiceScopedLifecycleContainerOn(Mock<IServiceProvider> provider, Times? times = null)
        => provider
            .Setup(x => x.GetService(typeof(ScopedLifecycleContainer)))
            .Returns(() => new ScopedLifecycleContainer([MockService1.Object, MockService2.Object, MockService3.Object], MockLifecycleViewController.Object, MockLogger))
            .Verifiable(times ?? Times.Once());

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_DuringReadyState_CallsRecycleScope()
    {
        // Arrange
        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider, Times.Exactly(2));

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        Expect_RecycleScopeAsyncOn(MockScopeProvider);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for first scope to enter steady state
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeReady(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);
        Expect_CleanupAsyncOn(MockService3);
        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        // Act: fire recycle signal during steady state
        signal.RequestRecycle();

        // Assert: RecycleScopeAsync is called (and RecyclingScope is logged before it)
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_RecyclingScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        MockScopeProvider.Verify(x => x.RecycleScopeAsync(), Times.Once);

        // Arrange
        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);
        Expect_CleanupAsyncOn(MockService3);

        // Stop to end the second scope
        Task stopTask = sut.StopAsync(default);
        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_DuringReadyState_LoopsToNextScope()
    {
        // Arrange
        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider, Times.Exactly(2));

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for steady state
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeReady(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));
        
        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);
        Expect_CleanupAsyncOn(MockService3);
        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);
        Expect_RecycleScopeAsyncOn(MockScopeProvider);

        // Act: fire recycle
        signal.RequestRecycle();

        // Assert: loop continues — second scope's ScopeReleased eventually logged
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_RecyclingScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // WaitingForScope appears twice: once at start and once after Reset
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_Initializing(MockService1.Object.Name), TimeSpan.FromSeconds(2))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(2), because: "loop should re-enter a new scope and start initializing again");

        // Arrange
        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);
        Expect_CleanupAsyncOn(MockService3);

        // Act
        Task stopTask = sut.StopAsync(default);

        // Assert
        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(2));

        int waitingForScopeCount = MockLogger.CountMessages(log => log.ApplicationLifecycle_WaitingForScope());
        waitingForScopeCount.Should().Be(2, because: "the loop should iterate twice: initial scope and post-recycle scope");
    }

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_DuringInit_CancelsAndCallsRecycleScope()
    {
        // Arrange: MockService2 init hangs until the signal fires and cancels it
        TaskCompletionSource hangingInitTcs = new();

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider, Times.Exactly(2));

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2, hangingInitTcs.Task);
        Expect_InitializeAsyncOn(MockService3);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for MockService2 to start initializing (confirming we are mid-init)
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_Initializing(MockService2.Object.Name), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);
        Expect_CleanupAsyncOn(MockService3);
        Expect_RecycleScopeAsyncOn(MockScopeProvider);
        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2, hangingInitTcs.Task);
        Expect_InitializeAsyncOn(MockService3);

        // Act: fire recycle while init is in progress
        signal.RequestRecycle();

        // Assert: cleanup starts (proves signal was processed)
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService3.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService3.Object.Name);
            log.ApplicationLifecycle_RecyclingScope();

            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
        });
    }

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_DoesNotReawaitPreInitializers()
    {
        // Arrange
        Mock<IPreScopeInitializer> mockPreInit = CreatePreScopeInitializer(nameof(mockPreInit));

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider, Times.Exactly(2));

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal, mockPreInit);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for first scope to reach steady state
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeReady(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);
        Expect_CleanupAsyncOn(MockService3);
        Expect_RecycleScopeAsyncOn(MockScopeProvider);
        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        // Act: fire recycle signal
        signal.RequestRecycle();

        // Wait for recycle to complete and loop to continue
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_RecyclingScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for the second scope to start initializing — confirms the loop re-entered without re-running pre-inits
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_WaitingForScope(), TimeSpan.FromSeconds(2))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(2));

        // Assert: pre-initializer was called exactly once (Times.Once is verified by VerifyMocks)
        mockPreInit.Verify(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_DuringCleanup_SecondSignalIsNoOp()
    {
        // Arrange: service1 cleanup hangs so we can observe the cleanup phase
        TaskCompletionSource cleanupTcs = new();

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider, Times.Exactly(2));

        Expect_InitializeAsyncAtLeastOnce(MockService1);
        Expect_InitializeAsyncAtLeastOnce(MockService2);
        Expect_InitializeAsyncAtLeastOnce(MockService3);
        Expect_CleanupAsyncAtLeastOnce(MockService1, cleanupTcs.Task);
        Expect_CleanupAsyncAtLeastOnce(MockService2);
        Expect_CleanupAsyncAtLeastOnce(MockService3);

        Expect_RecycleScopeAsyncOn(MockScopeProvider);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeReady(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Act: first recycle during steady state
        signal.RequestRecycle();

        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Verify not recycled yet while cleanup is in progress
        MockScopeProvider.Verify(x => x.RecycleScopeAsync(), Times.Never);

        // Second signal during cleanup — already cancelled, so this is a no-op
        signal.RequestRecycle();

        // Complete cleanup — recycle should proceed exactly once
        cleanupTcs.SetResult();

        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_RecyclingScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        MockScopeProvider.Verify(x => x.RecycleScopeAsync(), Times.Once);

        Task stopTask = sut.StopAsync(default);
        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_DuringReadyState_BlocksUntilCleanupCompletes()
    {
        // Arrange: service1 cleanup hangs so we can verify RecycleScopeAsync is not called prematurely
        TaskCompletionSource cleanupTcs = new();

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider, Times.Exactly(2));

        Expect_InitializeAsyncAtLeastOnce(MockService1);
        Expect_InitializeAsyncAtLeastOnce(MockService2);
        Expect_InitializeAsyncAtLeastOnce(MockService3);
        Expect_CleanupAsyncAtLeastOnce(MockService1, cleanupTcs.Task);
        Expect_CleanupAsyncAtLeastOnce(MockService2);
        Expect_CleanupAsyncAtLeastOnce(MockService3);

        Expect_RecycleScopeAsyncOn(MockScopeProvider);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeReady(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Act: fire recycle — cleanup starts but is incomplete
        signal.RequestRecycle();

        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // RecycleScopeAsync must not be called while cleanup is still in progress
        MockScopeProvider.Verify(x => x.RecycleScopeAsync(), Times.Never);

        // Complete cleanup — RecycleScopeAsync should now be called
        cleanupTcs.SetResult();

        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_RecyclingScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        MockScopeProvider.Verify(x => x.RecycleScopeAsync(), Times.Once);

        Task stopTask = sut.StopAsync(default);
        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_AfterRecycle_WaitsForInitializationBeforeReady()
    {
        // Arrange: after recycle, service2 init hangs in the second scope
        int service2InitCalls = 0;
        TaskCompletionSource service2SecondInitTcs = new();

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider, Times.Exactly(2));

        Expect_InitializeAsyncAtLeastOnce(MockService1);
        MockService2
            .Setup(x => x.InitializeAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(delegate (IInvocation invocation)
            {
                TaskCompletionSource tcs = new();
                foreach (object arg in invocation.Arguments)
                {
                    if (arg is CancellationToken ct)
                    {
                        ct.Register(() => tcs.TrySetCanceled());
                        break;
                    }
                }
                if (Interlocked.Increment(ref service2InitCalls) == 1)
                {
                    tcs.TrySetResult();
                }
                else
                {
                    service2SecondInitTcs.Task.ContinueWith(_ => tcs.TrySetResult(), TaskContinuationOptions.ExecuteSynchronously);
                }
                return tcs.Task;
            })
            .Verifiable(Times.AtLeast(2));
        Expect_InitializeAsyncAtLeastOnce(MockService3);
        Expect_CleanupAsyncAtLeastOnce(MockService1);
        Expect_CleanupAsyncAtLeastOnce(MockService2);
        Expect_CleanupAsyncAtLeastOnce(MockService3);

        Expect_RecycleScopeAsyncOn(MockScopeProvider);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for first scope to reach steady state, then recycle
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeReady(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        signal.RequestRecycle();

        // Wait for second scope init to start
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_Initializing(MockService2.Object.Name), TimeSpan.FromSeconds(2))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(2), because: "second scope should begin initializing after recycle");

        // Assert: scope is not yet ready (still initializing service2)
        MockLogger.CountMessages(log => log.ApplicationLifecycle_ScopeReady())
            .Should().Be(1, because: "ScopeReady should only appear once — the second scope is still initializing");
    }

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_ErrorDuringCleanup_ContinuesToRecycle()
    {
        // Arrange: service2 cleanup throws; lifecycle should log the error but still call RecycleScopeAsync
        Exception cleanupError = new InvalidOperationException("Cleanup failure");

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider, Times.Exactly(2));

        Expect_InitializeAsyncAtLeastOnce(MockService1);
        Expect_InitializeAsyncAtLeastOnce(MockService2);
        Expect_InitializeAsyncAtLeastOnce(MockService3);
        Expect_CleanupAsyncAtLeastOnce(MockService1);
        Expect_CleanupAsyncAtLeastOnce(MockService2, Task.FromException(cleanupError));
        Expect_CleanupAsyncAtLeastOnce(MockService3);
        // SetFatalError is called once per failing cleanup; service2 cleanup fails in each scope iteration
        Expect_SetFatalErrorOn(MockActivity, cleanupError, cleanupError);

        Expect_RecycleScopeAsyncOn(MockScopeProvider);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeReady(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Act
        signal.RequestRecycle();

        // Assert: error is logged and RecycleScopeAsync is still called despite the cleanup failure
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_CleaningUpFailed(MockService2.Object.Name, cleanupError), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_RecyclingScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        MockScopeProvider.Verify(x => x.RecycleScopeAsync(), Times.Once);

        Task stopTask = sut.StopAsync(default);
        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_AfterRecycle_ErrorDuringInit_ExitsLoop()
    {
        // Arrange: service2 init succeeds in first scope but fails in second scope after recycle
        Exception initError = new InvalidOperationException("Init failure after recycle");
        int service2InitCalls = 0;

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider, Times.Exactly(2));

        Expect_InitializeAsyncAtLeastOnce(MockService1);
        MockService2
            .Setup(x => x.InitializeAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(delegate (IInvocation invocation)
            {
                return Interlocked.Increment(ref service2InitCalls) == 1
                    ? Task.CompletedTask
                    : Task.FromException(initError);
            })
            .Verifiable(Times.AtLeast(2));
        Expect_InitializeAsyncOn(MockService3); // only initialized in first scope (second scope aborts before service3)
        Expect_CleanupAsyncAtLeastOnce(MockService1);
        Expect_CleanupAsyncAtLeastOnce(MockService2);
        Expect_CleanupAsyncOn(MockService3); // only cleaned up in first scope

        Expect_SetFatalErrorOn(MockActivity, initError);

        Expect_RecycleScopeAsyncOn(MockScopeProvider);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for first scope to reach steady state, then recycle
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeReady(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        signal.RequestRecycle();

        // Wait for second scope to fail and exit the loop
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ShuttingDown(), TimeSpan.FromSeconds(2))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(2), because: "init failure in second scope should exit the loop");

        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_InitializingFailed(MockService2.Object.Name, initError), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        Task stopTask = sut.StopAsync(default);
        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_DuringPreInit_IsNoOp()
    {
        // Arrange: pre-init hangs; signal fires while it is waiting
        TaskCompletionSource preInitTcs = new();
        Mock<IPreScopeInitializer> mockPreInit = CreatePreScopeInitializer(nameof(mockPreInit), preInitTcs.Task);

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal, mockPreInit);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Fire signal while pre-init is still waiting — no scope exists yet to recycle
        signal.RequestRecycle();

        // Complete pre-init — scope should be created normally despite the signal
        preInitTcs.SetResult();

        // Assert: scope reaches ready state (lifecycle continues normally after the no-op recycle)
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeReady(), TimeSpan.FromSeconds(2))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(2), because: "scope should be created normally after pre-init completes");
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_WaitsForPreInitializers()
    {
        // Arrange
        Mock<IPreScopeInitializer> mockPreInit = CreatePreScopeInitializer(nameof(mockPreInit));

        ApplicationLifecycle sut = CreateSut(mockPreInit);

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        // Act
        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for pre-initializer to be called
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_WaitingForScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_ScopeReady();
        });
        mockPreInit.Verify();
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_PreInitializerDelayed_WaitsBeforeStartingScope()
    {
        // Arrange
        TaskCompletionSource preInitTcs = new();
        Mock<IPreScopeInitializer> mockPreInit = CreatePreScopeInitializer(nameof(mockPreInit));

        ApplicationLifecycle sut = CreateSut(mockPreInit);

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        // Act
        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Pre-initializer hasn't completed, so scope shouldn't be created yet
        MockLogger.Messages.Should().NotContain(m => m.Contains("WaitingForScope"));

        // Complete pre-initializer
        preInitTcs.SetResult();

        // Assert
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_WaitingForScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_ScopeReady();
        });

        mockPreInit.Verify();
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_MultiplePreInitializers_WaitsForAll()
    {
        // Arrange
        Mock<IPreScopeInitializer> mockPreInit1 = CreatePreScopeInitializer(nameof(mockPreInit1));
        Mock<IPreScopeInitializer> mockPreInit2 = CreatePreScopeInitializer(nameof(mockPreInit2));
        Mock<IPreScopeInitializer> mockPreInit3 = CreatePreScopeInitializer(nameof(mockPreInit3));

        ApplicationLifecycle sut = CreateSut(mockPreInit1, mockPreInit2, mockPreInit3);

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        // Act
        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for scope to be ready
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_WaitingForScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_ScopeReady();
        });

        mockPreInit1.Verify();
        mockPreInit2.Verify();
        mockPreInit3.Verify();
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_PreInitializerFails_SetsActivityError()
    {
        // Arrange
        Exception expectedError = new InvalidOperationException("PreInit failed");
        Mock<IPreScopeInitializer> mockPreInit = CreatePreScopeInitializer(nameof(mockPreInit), Task.FromException(expectedError));

        Expect_SetFatalErrorOn(MockActivity, expectedError);

        ApplicationLifecycle sut = CreateSut(mockPreInit);

        // Act
        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Assert - ExecuteTask should fault with unhandled error
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_PreinitializerFailed(mockPreInit.Object.Name, expectedError);
            log.ApplicationLifecycle_ShuttingDown();
        });

        mockPreInit.Verify();
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_LastPreInitializerFails_StopsBeforeScope()
    {
        // Arrange
        Exception expectedError = new InvalidOperationException("Last PreInit failed");
        Mock<IPreScopeInitializer> mockPreInit1 = CreatePreScopeInitializer(nameof(mockPreInit1));
        Mock<IPreScopeInitializer> mockPreInit2 = CreatePreScopeInitializer(nameof(mockPreInit2), Task.FromException(expectedError));

        ApplicationLifecycle sut = CreateSut(mockPreInit1, mockPreInit2);

        // Act
        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Assert - execution should fail before creating scope
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_PreinitializerFailed(mockPreInit2.Object.Name, expectedError), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));
        MockLogger.Messages.Should().NotContain(m => m.Contains("WaitingForScope"));
        mockPreInit1.Verify();
        mockPreInit2.Verify();
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_PreInitializerCreatesActivityForEach()
    {
        // Arrange
        Mock<IPreScopeInitializer> mockPreInit = CreatePreScopeInitializer(nameof(mockPreInit));
        Mock<ILifecycleActivity> mockActivity = new();

        MockLifecycleViewController
            .Setup(x => x.StartTask(Phrases.Startup_Preinitializing(mockPreInit.Object.Name)))
            .Returns(mockActivity.Object)
            .Verifiable(Times.Once);

        ApplicationLifecycle sut = CreateSut(mockPreInit);

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        // Act
        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for pre-initializer to be called
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_WaitingForScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Assert - StartTask should have been called for the pre-initializer
        MockLifecycleViewController.Verify();
        mockPreInit.Verify();

        // Assert - Activity should be disposed after pre-initializer completes
        mockActivity.Verify(x => x.Dispose(), Times.Once, "Activity should be disposed after pre-initializer completes");

        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_ScopeReady();
        });
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_PreInitializerActivity_DisposesImmediatelyWhenCompleted()
    {
        // Arrange
        TaskCompletionSource slowPreInitTcs = new();
        Mock<IPreScopeInitializer> fastPreInit = CreatePreScopeInitializer(nameof(fastPreInit));
        Mock<IPreScopeInitializer> slowPreInit = CreatePreScopeInitializer(nameof(slowPreInit), slowPreInitTcs.Task);
        Mock<ILifecycleActivity> fastActivity = new();
        Mock<ILifecycleActivity> slowActivity = new();

        int callCount = 0;

        // Mock StartTask to return different activities based on call order
        MockLifecycleViewController
            .Setup(x => x.StartTask(It.IsAny<string>()))
            .Returns(() =>
            {
                if (callCount == 0)
                {
                    callCount++;
                    return fastActivity.Object;
                }
                else if (callCount == 1)
                {
                    callCount++;
                    return slowActivity.Object;
                }
                return MockActivity.Object;
            });

        ApplicationLifecycle sut = CreateSut(fastPreInit, slowPreInit);

        Expect_GetServiceScopedLifecycleContainerOn(MockServiceProvider);

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        // Act
        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Give time for fast pre-initializer to complete and be disposed
        Thread.Sleep(100);

        // Assert - Fast activity should be disposed even though slow activity is still pending
        fastActivity.Verify(x => x.Dispose(), Times.Once, "Fast activity should be disposed immediately after completing");
        slowActivity.Verify(x => x.Dispose(), Times.Never, "Slow activity should not be disposed while still pending");

        // Complete slow pre-initializer
        slowPreInitTcs.SetResult();

        // Wait for scope to start
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_WaitingForScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Assert - Now slow activity should be disposed too
        slowActivity.Verify(x => x.Dispose(), Times.Once, "Slow activity should be disposed after completing");

        // The first log message is for waiting for the slow preinitializer
        MockLogger.VerifyMessages(log =>
        {
            log.ApplicationLifecycle_WaitingForPreinitializer("slowPreInit");
            log.ApplicationLifecycle_WaitingForScope();
            log.ApplicationLifecycle_Initializing(MockService1.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService1.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService2.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService2.Object.Name);
            log.ApplicationLifecycle_Initializing(MockService3.Object.Name);
            log.ApplicationLifecycle_Initialized(MockService3.Object.Name);
            log.ApplicationLifecycle_ScopeReady();
        });
    }
}
