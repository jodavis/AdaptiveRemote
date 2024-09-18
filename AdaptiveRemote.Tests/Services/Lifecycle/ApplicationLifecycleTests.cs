using AdaptiveRemote.Logging;
using Moq;

namespace AdaptiveRemote.Services.Lifecycle;

[TestClass]
public class ApplicationLifecycleTests
{
    private static readonly Task IncompleteTask = new TaskCompletionSource().Task;

    private readonly Mock<IScopedLifecycle> MockService1 = new();
    private readonly Mock<IScopedLifecycle> MockService2 = new();
    private readonly Mock<IScopedLifecycle> MockService3 = new();

    private readonly Mock<IApplicationScopeFactory> MockScopeFactory = new();
    private readonly Mock<IApplicationScope> MockScope = new();
    private readonly Mock<IServiceProvider> MockServiceProvider = new();
    private readonly MockLogger<ApplicationLifecycle> MockLogger = new();

    private ApplicationLifecycle CreateSut() => new ApplicationLifecycle(MockScopeFactory.Object, MockLogger);

    [TestInitialize]
    public void SetupMocks()
    {
        MockScopeFactory
            .Setup(x => x.CreateNewScopeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(MockScope.Object))
            .Verifiable(Times.Once);

        MockScope
            .Setup(x => x.TryInvokeAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(delegate (Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken)
            {
                return workItem.Invoke(MockServiceProvider.Object, cancellationToken);
            });

        MockServiceProvider
            .Setup(x => x.GetService(typeof(IEnumerable<IScopedLifecycle>)))
            .Returns(new[] { MockService1.Object, MockService2.Object, MockService3.Object })
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
            .Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        MockService2
            .Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        MockService3
            .Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);

        MockService1
            .Setup(x => x.CleanUpAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        MockService2
            .Setup(x => x.CleanUpAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        MockService3
            .Setup(x => x.CleanUpAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        Verify(MockServiceProvider, nameof(MockServiceProvider));
        Verify(MockScopeFactory, nameof(MockScopeFactory));
        Verify(MockScope, nameof(MockScope));

        Verify(MockService1, nameof(MockService1));
        Verify(MockService2, nameof(MockService2));
        Verify(MockService3, nameof(MockService3));

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
        TaskAssert.IsComplete(startTask, nameof(startTask));
        TaskAssert.IsNotComplete(sut.ExecuteTask, nameof(sut.ExecuteTask));

        MockLogger.VerifyMessages(
            Expect_InitializingMessage(MockService1),
            Expect_InitializedMessage(MockService1),
            Expect_InitializingMessage(MockService2),
            Expect_InitializedMessage(MockService2),
            Expect_InitializingMessage(MockService3),
            Expect_InitializedMessage(MockService3));
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
        TaskAssert.IsComplete(startTask, nameof(startTask));
        TaskAssert.IsNotComplete(sut.ExecuteTask, nameof(sut.ExecuteTask));

        MockLogger.VerifyMessages(
            Expect_InitializingMessage(MockService1),
            Expect_InitializingMessage(MockService2),
            Expect_InitializingMessage(MockService3));
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_LogsErrorButStartsOtherServices()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Exception expectedError1 = new InvalidOperationException("Error 1");
        Exception expectedError2 = new FormatException("Error 2");

        Expect_InitializeAsyncOn(MockService1, Task.FromException(expectedError1));
        Expect_InitializeAsyncOn(MockService2, Task.FromException(expectedError2));
        Expect_InitializeAsyncOn(MockService3, IncompleteTask);

        // Act
        Task startTask = sut.StartAsync(default);

        // Assert
        TaskAssert.IsComplete(startTask, nameof(startTask));
        TaskAssert.IsNotComplete(sut.ExecuteTask, nameof(sut.ExecuteTask));

        MockLogger.VerifyMessages(
            Expect_InitializingMessage(MockService1),
            Expect_InitializingFailedMessage(MockService1, expectedError1),
            Expect_InitializingMessage(MockService2),
            Expect_InitializingFailedMessage(MockService2, expectedError2),
            Expect_InitializingMessage(MockService3));
    }

    [TestMethod]
    public void ApplicationLifecycle_StopAsync_DisposesScopeAndCompletesTask()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);
        Expect_DisposeScope();
        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);
        Expect_CleanupAsyncOn(MockService3);

        Task startTask = sut.StartAsync(default);

        // Act
        Task stopTask = sut.StopAsync(default);

        // Assert
        TaskAssert.IsComplete(startTask, nameof(startTask));
        TaskAssert.IsComplete(stopTask, nameof(stopTask));
        TaskAssert.IsComplete(sut.ExecuteTask, nameof(sut.ExecuteTask));

        MockLogger.VerifyMessages(
            Expect_InitializingMessage(MockService1),
            Expect_InitializedMessage(MockService1),
            Expect_InitializingMessage(MockService2),
            Expect_InitializedMessage(MockService2),
            Expect_InitializingMessage(MockService3),
            Expect_InitializedMessage(MockService3),
            Expect_ShuttingDownMessage,
            Expect_CleaningUpMessage(MockService1),
            Expect_CleanedUpMessage(MockService1),
            Expect_CleaningUpMessage(MockService2),
            Expect_CleanedUpMessage(MockService2),
            Expect_CleaningUpMessage(MockService3),
            Expect_CleanedUpMessage(MockService3));
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

        // Act
        Task stopTask = sut.StopAsync(default);

        // Assert
        TaskAssert.IsComplete(startTask, nameof(startTask));
        TaskAssert.IsNotComplete(stopTask, nameof(stopTask));
        TaskAssert.IsNotComplete(sut.ExecuteTask, nameof(sut.ExecuteTask));

        MockLogger.VerifyMessages(
            Expect_InitializingMessage(MockService1),
            Expect_InitializedMessage(MockService1),
            Expect_InitializingMessage(MockService2),
            Expect_InitializedMessage(MockService2),
            Expect_InitializingMessage(MockService3),
            Expect_InitializedMessage(MockService3),
            Expect_ShuttingDownMessage,
            Expect_CleaningUpMessage(MockService1),
            Expect_CleaningUpMessage(MockService2),
            Expect_CleaningUpMessage(MockService3));
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
        Expect_DisposeScope();
        Expect_CleanupAsyncOn(MockService1, result: Task.FromException(expectedError1));
        Expect_CleanupAsyncOn(MockService2, result: Task.FromException(expectedError2));
        Expect_CleanupAsyncOn(MockService3);

        Task startTask = sut.StartAsync(default);

        // Act
        Task stopTask = sut.StopAsync(default);

        // Assert
        TaskAssert.IsComplete(startTask, nameof(startTask));
        TaskAssert.IsComplete(stopTask, nameof(stopTask));
        TaskAssert.IsComplete(sut.ExecuteTask, nameof(sut.ExecuteTask));

        MockLogger.VerifyMessages(
            Expect_InitializingMessage(MockService1),
            Expect_InitializedMessage(MockService1),
            Expect_InitializingMessage(MockService2),
            Expect_InitializedMessage(MockService2),
            Expect_InitializingMessage(MockService3),
            Expect_InitializedMessage(MockService3),
            Expect_ShuttingDownMessage,
            Expect_CleaningUpMessage(MockService1),
            Expect_CleaningUpFailedMessage(MockService1, expectedError1),
            Expect_CleaningUpMessage(MockService2),
            Expect_CleaningUpFailedMessage(MockService2, expectedError2),
            Expect_CleaningUpMessage(MockService3),
            Expect_CleanedUpMessage(MockService3));
    }

    [TestMethod]
    public void ApplicationLifecycle_StopAsync_CancelsInitializeMethodsThatAreWaiting()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2, result: IncompleteTask);
        Expect_InitializeAsyncOn(MockService3);
        Expect_DisposeScope();
        Expect_CleanupAsyncOn(MockService1);
        Expect_CleanupAsyncOn(MockService2);
        Expect_CleanupAsyncOn(MockService3);

        Task startTask = sut.StartAsync(default);

        // Act
        Task stopTask = sut.StopAsync(default);

        // Assert
        TaskAssert.IsComplete(startTask, nameof(startTask));
        TaskAssert.IsComplete(stopTask, nameof(stopTask));
        TaskAssert.IsComplete(sut.ExecuteTask, nameof(sut.ExecuteTask));

        MockLogger.VerifyMessages(
            Expect_InitializingMessage(MockService1),
            Expect_InitializedMessage(MockService1),
            Expect_InitializingMessage(MockService2),
            Expect_InitializingMessage(MockService3),
            Expect_InitializedMessage(MockService3),
            Expect_ShuttingDownMessage,
            Expect_CleaningUpMessage(MockService1),
            Expect_CleanedUpMessage(MockService1),
            Expect_CleaningUpMessage(MockService2),
            Expect_CleanedUpMessage(MockService2),
            Expect_CleaningUpMessage(MockService3),
            Expect_CleanedUpMessage(MockService3));
    }

    private void Expect_DisposeScope()
        => MockScope
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

    private static void Expect_InitializeAsyncOn(Mock<IScopedLifecycle> service, Task? result = default)
        => service
            .Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(result)
            .Verifiable(Times.Once);

    private static void Expect_CleanupAsyncOn(Mock<IScopedLifecycle> service, Task? result = default)
        => service
            .Setup(x => x.CleanUpAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(result)
            .Verifiable(Times.Once);

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
    private static string Expect_ShuttingDownMessage
        => $"Information[707]: {LoggingMessages.ApplicationLifecycle_ShuttingDown}";
}
