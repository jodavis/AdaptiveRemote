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

    private ApplicationLifecycle CreateSut() => new ApplicationLifecycle(
        MockScopeProvider.Object,
        MockLifecycleViewController.Object,
        MockSignal.Object,
        [],  // Empty IPreScopeInitializer collection
        MockLogger);

    [TestInitialize]
    public void SetupMocks()
    {
        MockScopeProvider
            .Setup(x => x.InvokeInScopeAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(delegate (Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken)
            {
                return workItem.Invoke(MockServiceProvider.Object, cancellationToken);
            });

        MockServiceProvider
            .Setup(x => x.GetService(typeof(ScopedLifecycleContainer)))
            .Returns(() => new ScopedLifecycleContainer([MockService1.Object, MockService2.Object, MockService3.Object], MockLifecycleViewController.Object, MockLogger))
            .Verifiable(Times.Between(1, 2, Moq.Range.Inclusive));

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
                throw new Exception($"Verify failed on {name}: {e.Message}");
            }
        }
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_StartsExecuteTaskAndInitializesScopedLifecycleServices()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

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
            log.ApplicationLifecycle_ScopeReleased();
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
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService2.Object.Name);
            log.ApplicationLifecycle_ScopeReleased();
        });

        startTask.Should().BeComplete(because: "StartAsync should complete after all services are initialized");
        sut.ExecuteTask.Should().NotBeComplete(because: "ExecuteTask should remain running after startup");
        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp, because: "Services are being cleaned up after failure");
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_DelayedFailure_LogsErrorAndDoesNotStartOtherServicesAndCleansUpServices()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Exception expectedError1 = new InvalidOperationException("Error 1");
        TaskCompletionSource tcs = new();

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
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService3.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService3.Object.Name);
            log.ApplicationLifecycle_ScopeReleased();
        });

        sut.ExecuteTask.Should().NotBeComplete(because: "ExecuteTask should remain running after startup");
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
            log.ApplicationLifecycle_ScopeReleased();
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
            log.ApplicationLifecycle_ScopeReleased();
            log.ApplicationLifecycle_ShuttingDown();
        });
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_ImmediateFailure_CancelsStartupThatIsInProgress()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Exception expectedError1 = new InvalidOperationException("Error 1");

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
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService2.Object.Name);
            log.ApplicationLifecycle_ScopeReleased();
        });

        startTask.Should().BeComplete(because: "StartAsync should complete after all services are initialized");
        sut.ExecuteTask.Should().NotBeComplete(because: "ExecuteTask should remain running after startup");
        LatestLifecyclePhase.Should().Be(LifecyclePhase.CleaningUp, because: "Services are being cleaned up after failure");
    }

    [TestMethod]
    public void ApplicationLifecycle_StopAsync_DisposesScopeAndCompletesTask()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

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
            log.ApplicationLifecycle_ScopeReleased();
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
            log.ApplicationLifecycle_ScopeReleased();
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
            log.ApplicationLifecycle_ScopeReleased();
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
            log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService1.Object.Name);
            log.ApplicationLifecycle_CleaningUp(MockService2.Object.Name);
            log.ApplicationLifecycle_CleanedUp(MockService2.Object.Name);
            log.ApplicationLifecycle_ScopeReleased();
            log.ApplicationLifecycle_ShuttingDown();
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

    private ApplicationLifecycle CreateSutWithSignal(IApplicationRecycleSignal signal, IEnumerable<IPreScopeInitializer>? preInitializers = null)
        => new ApplicationLifecycle(
            MockScopeProvider.Object,
            MockLifecycleViewController.Object,
            signal,
            preInitializers ?? [],
            MockLogger);

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_DuringReadyState_CallsRecycleScope()
    {
        // Arrange: allow services to initialize and clean up in two scope iterations
        MockServiceProvider
            .Setup(x => x.GetService(typeof(ScopedLifecycleContainer)))
            .Returns(() => new ScopedLifecycleContainer([MockService1.Object, MockService2.Object, MockService3.Object], MockLifecycleViewController.Object, MockLogger))
            .Verifiable(Times.AtLeast(1));

        Expect_InitializeAsyncAtLeastOnce(MockService1);
        Expect_InitializeAsyncAtLeastOnce(MockService2);
        Expect_InitializeAsyncAtLeastOnce(MockService3);
        Expect_CleanupAsyncAtLeastOnce(MockService1);
        Expect_CleanupAsyncAtLeastOnce(MockService2);
        Expect_CleanupAsyncAtLeastOnce(MockService3);

        MockScopeProvider
            .Setup(x => x.RecycleScopeAsync())
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for first scope to enter steady state
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeReleased(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Act: fire recycle signal during steady state
        signal.RequestRecycle();

        // Assert: RecycleScopeAsync is called (and RecyclingScope is logged before it)
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_RecyclingScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        MockScopeProvider.Verify(x => x.RecycleScopeAsync(), Times.Once);

        // Stop to end the second scope
        Task stopTask = sut.StopAsync(default);
        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_DuringReadyState_LoopsToNextScope()
    {
        // Arrange
        MockServiceProvider
            .Setup(x => x.GetService(typeof(ScopedLifecycleContainer)))
            .Returns(() => new ScopedLifecycleContainer([MockService1.Object, MockService2.Object, MockService3.Object], MockLifecycleViewController.Object, MockLogger))
            .Verifiable(Times.AtLeast(1));

        Expect_InitializeAsyncAtLeastOnce(MockService1);
        Expect_InitializeAsyncAtLeastOnce(MockService2);
        Expect_InitializeAsyncAtLeastOnce(MockService3);
        Expect_CleanupAsyncAtLeastOnce(MockService1);
        Expect_CleanupAsyncAtLeastOnce(MockService2);
        Expect_CleanupAsyncAtLeastOnce(MockService3);

        MockScopeProvider
            .Setup(x => x.RecycleScopeAsync())
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for steady state
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeReleased(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Act: fire recycle
        signal.RequestRecycle();

        // Assert: loop continues — second scope's ScopeReleased eventually logged
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_RecyclingScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // WaitingForScope appears twice: once at start and once after Reset
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_Initializing(MockService1.Object.Name), TimeSpan.FromSeconds(2))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(2), because: "loop should re-enter a new scope and start initializing again");

        Task stopTask = sut.StopAsync(default);
        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(2));

        int waitingForScopeCount = MockLogger.Messages.Count(m => m.Contains("Waiting for application scope"));
        waitingForScopeCount.Should().Be(2, because: "the loop should iterate twice: initial scope and post-recycle scope");
    }

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_DuringInit_DoesNotCallRecycleScope()
    {
        // Arrange: MockService2 init hangs until the signal fires and cancels it
        TaskCompletionSource hangingInitTcs = new();

        MockServiceProvider
            .Setup(x => x.GetService(typeof(ScopedLifecycleContainer)))
            .Returns(() => new ScopedLifecycleContainer([MockService1.Object, MockService2.Object, MockService3.Object], MockLifecycleViewController.Object, MockLogger))
            .Verifiable(Times.AtLeast(1));

        Expect_InitializeAsyncAtLeastOnce(MockService1);
        Expect_InitializeAsyncAtLeastOnce(MockService2, hangingInitTcs.Task);
        Expect_InitializeAsyncAtLeastOnce(MockService3);
        Expect_CleanupAsyncAtLeastOnce(MockService1);
        Expect_CleanupAsyncAtLeastOnce(MockService2);
        Expect_CleanupAsyncAtLeastOnce(MockService3);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for MockService2 to start initializing (confirming we are mid-init)
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_Initializing(MockService2.Object.Name), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Act: fire recycle while init is in progress
        signal.RequestRecycle();

        // Assert: cleanup starts (proves signal was processed)
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_CleaningUp(MockService1.Object.Name), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // RecycleScopeAsync must NOT have been called (init was not complete)
        MockScopeProvider.Verify(x => x.RecycleScopeAsync(), Times.Never);

        // Stop to end the second scope attempt
        Task stopTask = sut.StopAsync(default);
        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void ApplicationLifecycle_RecycleSignal_DoesNotReawaitPreInitializers()
    {
        // Arrange
        Mock<IPreScopeInitializer> mockPreInit = new();
        mockPreInit
            .Setup(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once); // Must be called exactly once, even after a recycle

        MockServiceProvider
            .Setup(x => x.GetService(typeof(ScopedLifecycleContainer)))
            .Returns(() => new ScopedLifecycleContainer([MockService1.Object, MockService2.Object, MockService3.Object], MockLifecycleViewController.Object, MockLogger))
            .Verifiable(Times.AtLeast(1));

        Expect_InitializeAsyncAtLeastOnce(MockService1);
        Expect_InitializeAsyncAtLeastOnce(MockService2);
        Expect_InitializeAsyncAtLeastOnce(MockService3);
        Expect_CleanupAsyncAtLeastOnce(MockService1);
        Expect_CleanupAsyncAtLeastOnce(MockService2);
        Expect_CleanupAsyncAtLeastOnce(MockService3);

        MockScopeProvider
            .Setup(x => x.RecycleScopeAsync())
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        ApplicationRecycleSignal signal = new();
        ApplicationLifecycle sut = CreateSutWithSignal(signal, [mockPreInit.Object]);

        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Wait for first scope to reach steady state
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_ScopeReleased(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Act: fire recycle signal
        signal.RequestRecycle();

        // Wait for recycle to complete and loop to continue
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_RecyclingScope(), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Give the loop time to start the second scope
        Thread.Sleep(100);

        // Assert: pre-initializer was called exactly once (Times.Once is verified by VerifyMocks)
        mockPreInit.Verify(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()), Times.Once);

        Task stopTask = sut.StopAsync(default);
        stopTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_WaitsForPreInitializers()
    {
        // Arrange
        Mock<IPreScopeInitializer> mockPreInit = new();
        mockPreInit
            .Setup(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        ApplicationLifecycle sut = new(MockScopeProvider.Object, MockLifecycleViewController.Object, MockSignal.Object, [mockPreInit.Object], MockLogger);

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
        mockPreInit.Verify();
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_PreInitializerDelayed_WaitsBeforeStartingScope()
    {
        // Arrange
        TaskCompletionSource preInitTcs = new();
        Mock<IPreScopeInitializer> mockPreInit = new();
        mockPreInit
            .Setup(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(preInitTcs.Task)
            .Verifiable(Times.Once);

        ApplicationLifecycle sut = new(MockScopeProvider.Object, MockLifecycleViewController.Object, MockSignal.Object, [mockPreInit.Object], MockLogger);

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
        mockPreInit.Verify();
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_MultiplePreInitializers_WaitsForAll()
    {
        // Arrange
        Mock<IPreScopeInitializer> mockPreInit1 = new();
        Mock<IPreScopeInitializer> mockPreInit2 = new();
        Mock<IPreScopeInitializer> mockPreInit3 = new();

        mockPreInit1
            .Setup(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        mockPreInit2
            .Setup(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        mockPreInit3
            .Setup(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        ApplicationLifecycle sut = new(
            MockScopeProvider.Object,
            MockLifecycleViewController.Object,
            MockSignal.Object,
            [mockPreInit1.Object, mockPreInit2.Object, mockPreInit3.Object],
            MockLogger);

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
        mockPreInit1.Verify();
        mockPreInit2.Verify();
        mockPreInit3.Verify();
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_PreInitializerFails_SetsActivityError()
    {
        // Arrange
        Exception expectedError = new InvalidOperationException("PreInit failed");
        Mock<IPreScopeInitializer> mockPreInit = new();

        mockPreInit
            .Setup(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(expectedError))
            .Verifiable(Times.Once);

        // Don't expect scope to be created when pre-init fails
        MockServiceProvider
            .Setup(x => x.GetService(typeof(ScopedLifecycleContainer)))
            .Verifiable(Times.Never);

        ApplicationLifecycle sut = new(MockScopeProvider.Object, MockLifecycleViewController.Object, MockSignal.Object, [mockPreInit.Object], MockLogger);

        // Act
        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Assert - ExecuteTask should fault with unhandled error
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_UnhandledError(expectedError), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));
        mockPreInit.Verify();
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_LastPreInitializerFails_StopsBeforeScope()
    {
        // Arrange
        Exception expectedError = new InvalidOperationException("Last PreInit failed");
        Mock<IPreScopeInitializer> mockPreInit1 = new();
        Mock<IPreScopeInitializer> mockPreInit2 = new();

        mockPreInit1
            .Setup(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        mockPreInit2
            .Setup(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(expectedError))
            .Verifiable(Times.Once);

        // Don't expect scope to be created when pre-init fails
        MockServiceProvider
            .Setup(x => x.GetService(typeof(ScopedLifecycleContainer)))
            .Verifiable(Times.Never);

        ApplicationLifecycle sut = new(
            MockScopeProvider.Object,
            MockLifecycleViewController.Object,
            MockSignal.Object,
            [mockPreInit1.Object, mockPreInit2.Object],
            MockLogger);

        // Act
        Task startTask = sut.StartAsync(default);
        startTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1));

        // Assert - execution should fail before creating scope
        MockLogger.WaitForMessageAsync(log => log.ApplicationLifecycle_UnhandledError(expectedError), TimeSpan.FromSeconds(1))
            .Should().BeCompleteWithin(TimeSpan.FromSeconds(1));
        MockLogger.Messages.Should().NotContain(m => m.Contains("WaitingForScope"));
        mockPreInit1.Verify();
        mockPreInit2.Verify();
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_PreInitializerCreatesActivityForEach()
    {
        // Arrange
        Mock<IPreScopeInitializer> mockPreInit = new();
        Mock<ILifecycleActivity> mockActivity = new();

        mockPreInit
            .Setup(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        MockLifecycleViewController
            .Setup(x => x.StartTask(It.Is<string>(s => s.Contains("CloudAssetOrchestrator") || s.Contains("PreScopeInitializer"))))
            .Returns(mockActivity.Object)
            .Verifiable(Times.Once);

        ApplicationLifecycle sut = new(MockScopeProvider.Object, MockLifecycleViewController.Object, MockSignal.Object, [mockPreInit.Object], MockLogger);

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
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_PreInitializerActivity_DisposesImmediatelyWhenCompleted()
    {
        // Arrange
        TaskCompletionSource slowPreInitTcs = new();
        Mock<IPreScopeInitializer> fastPreInit = new();
        Mock<IPreScopeInitializer> slowPreInit = new();
        Mock<ILifecycleActivity> fastActivity = new();
        Mock<ILifecycleActivity> slowActivity = new();

        int callCount = 0;

        fastPreInit
            .Setup(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        slowPreInit
            .Setup(x => x.WaitAsync(It.IsAny<ILifecycleActivity>(), It.IsAny<CancellationToken>()))
            .Returns(slowPreInitTcs.Task)
            .Verifiable(Times.Once);

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

        ApplicationLifecycle sut = new(
            MockScopeProvider.Object,
            MockLifecycleViewController.Object,
            MockSignal.Object,
            [fastPreInit.Object, slowPreInit.Object],
            MockLogger);

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
    }

}
