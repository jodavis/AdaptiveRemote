using System.Net;
using AdaptiveRemote.Models;
using FluentAssertions;
using Moq;

namespace AdaptiveRemote.Services.TiVo;

[TestClass]
public class TiVoServiceTests
{
    private readonly Mock<ITiVoLocator> MockLocator = new();
    private readonly Mock<ITiVoConnection.Factory> MockConnectionFactory = new();
    private readonly Mock<ITiVoConnection> MockConnection = new();
    private readonly Mock<IRemoteDefinitionService> MockDefinition = new();
    private readonly Mock<ILifecycleActivity> MockInitializeActivity = new() { Name = nameof(MockInitializeActivity) };
    private readonly Mock<ILifecycleActivity> MockCleanupActivity = new() { Name = nameof(MockCleanupActivity) };
    private readonly MockLogger<TiVoService> MockLogger = new();

    private readonly LayoutGroup Commands = new("ROOT",
        [
            new LifecycleCommand("UNUSED1"),
            new TiVoCommand("Play"),
            new TiVoCommand("Stop"),
            new LifecycleCommand("UNUSED2"),
        ]);
    private TiVoCommand PlayCommand => Commands.Elements.OfType<TiVoCommand>().First();

    private ILifecycleActivity InitializeActivity => MockInitializeActivity.Object;
    private ILifecycleActivity CleanupActivity => MockCleanupActivity.Object;

    private TiVoService CreateUninitializedSut() => new(MockLocator.Object, MockConnectionFactory.Object, MockDefinition.Object, MockLogger);
    private TiVoService CreateSut()
    {
        const int InitializeTimeoutInMilliseconds = 5000;
        TiVoService sut = CreateUninitializedSut();

        ((IScopedLifecycle)sut).InitializeAsync(InitializeActivity, default)
            .Should().BeCompleteWithin(TimeSpan.FromMilliseconds(InitializeTimeoutInMilliseconds),
                because: nameof(TiVoService) + " should initialize within that time");

        return sut;
    }

    [TestInitialize]
    public void SetupMocks()
    {
        MockEndPoint mockEndPoint = new();

        MockLocator
            .Setup(x => x.FindTiVoAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<EndPoint>(mockEndPoint))
            .Verifiable(Times.Once);

        MockConnectionFactory
            .Setup(x => x.ConnectAsync(It.IsAny<EndPoint>(), It.IsAny<CancellationToken>()))
            .Callback(delegate (EndPoint ep, CancellationToken cancel)
            {
                ep.Should().BeSameAs(mockEndPoint, "the endpoint from " + nameof(MockLocator) + " should be passed to " + nameof(MockConnectionFactory));
            })
            .Returns(Task.FromResult(MockConnection.Object))
            .Verifiable(Times.Once);

        MockConnection
            .Setup(x => x.SendIRCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(delegate (string commandId, CancellationToken cancellation)
            {
                Assert.Fail(string.Format("Did not expect {0}.{1}(\"{2}\")",
                    nameof(ITiVoConnection),
                    nameof(ITiVoConnection.SendIRCommandAsync),
                    commandId));
            });
        MockConnection
            .Setup(x => x.DisposeAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);

        MockDefinition
            .SetupGet(x => x.RemoteRoot)
            .Returns(Commands)
            .Verifiable(Times.Once);

        MockInitializeActivity
            .SetupSet(x => x.Description = It.IsAny<string>())
            .Verifiable(Times.Never);
        MockInitializeActivity
            .SetupSet(x => x.Description = "Connecting to TiVo")
            .Verifiable(Times.Once);
        MockInitializeActivity
            .Setup(x => x.SetFatalError(It.IsAny<Exception>()))
            .Callback(delegate (Exception ex) { Assert.Fail(string.Format("SetFatalError was called on the activity: {0}", ex)); });
        MockInitializeActivity
            .Setup(x => x.Dispose())
            .Verifiable(Times.Never);

        MockCleanupActivity
            .SetupSet(x => x.Description = It.IsAny<string>())
            .Verifiable(Times.Never);
        MockCleanupActivity
            .Setup(x => x.SetFatalError(It.IsAny<Exception>()))
            .Callback(delegate (Exception ex) { Assert.Fail(string.Format("SetFatalError was called on the activity: {0}", ex)); });
        MockCleanupActivity
            .Setup(x => x.Dispose())
            .Verifiable(Times.Never);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockLocator.Verify();
        MockConnectionFactory.Verify();
        MockConnection.Verify();
        MockDefinition.Verify();
        MockInitializeActivity.Verify();
        MockCleanupActivity.Verify();
    }

    [TestMethod]
    public void TiVoService_Constructor_DoesNotCreateTiVoConnection()
    {
        // Arrange
        Expect_Locator_IsNotCalled();
        Expect_MockConnectionFactory_IsNotCalled();
        Expect_InitializationActivity_Description_IsNotCalled();

        // Act
        _ = CreateUninitializedSut();

        // Assert
        // Constructor should store services but not act on any of them
    }

    [TestMethod]
    public void TiVoService_Name_ReturnsAName()
    {
        // Arrange
        Expect_Locator_IsNotCalled();
        Expect_MockConnectionFactory_IsNotCalled();
        Expect_InitializationActivity_Description_IsNotCalled();

        IScopedLifecycle sut = CreateUninitializedSut();

        // Act
        string result = sut.Name;

        // Assert
        result.Should().NotBeNullOrEmpty(because: "the Name property should return something");
    }

    [TestMethod]
    public void TiVoService_InitializeAsync_CreatesTiVoConnectionAndEnablesCommands()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        // Act
        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        initializeTask.Should().BeComplete(because: "InitializeAsync should complete after TiVo service is initialized");

        foreach (TiVoCommand command in Commands.Elements.OfType<TiVoCommand>())
        {
            command.ExecuteAsync.Should().NotBeNull(
                because: "the service should have set it on the {0} command", command.Name);
            command.IsEnabled.Should().BeTrue(
                because: "the service should have enabled the {0} command", command.Name);
        }
    }

    [TestMethod]
    [Timeout(100)]
    public void TiVoService_InitializeAsync_WaitsForTiVoLocator()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        MockLocator
            .Setup(x => x.FindTiVoAsync(It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource<EndPoint>().Task);

        Expect_MockConnectionFactory_IsNotCalled();

        // Act
        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        initializeTask.Should().NotBeComplete(because: "InitializeAsync should wait for locator to complete");
    }

    [TestMethod]
    [Timeout(100)]
    public void TiVoService_InitializeAsync_ConinuesWhenTiVoLocatorCompletes()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        EndPoint expectedEndPoint = MockLocator.Object.FindTiVoAsync(default).Result;

        TaskCompletionSource<EndPoint> tcs = new();
        MockLocator
            .Setup(x => x.FindTiVoAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        // Act
        tcs.SetResult(expectedEndPoint);

        // Assert
        initializeTask.Should().BeComplete(because: "InitializeAsync should complete after locator returns an endpoint");
    }

    [TestMethod]
    [Timeout(100)]
    public void TiVoService_InitializeAsync_WaitsForTiVoConnectionFactory()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        MockConnectionFactory
            .Setup(x => x.ConnectAsync(It.IsAny<EndPoint>(), It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource<ITiVoConnection>().Task);

        // Act
        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        initializeTask.Should().NotBeComplete(because: "InitializeAsync should wait for connection factory to complete");
    }

    [TestMethod]
    [Timeout(100)]
    public void TiVoService_InitializeAsync_ContinuesWhenTiVoConnectionFactoryCompletes()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        TaskCompletionSource<ITiVoConnection> tcs = new();
        MockConnectionFactory
            .Setup(x => x.ConnectAsync(It.IsAny<EndPoint>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        // Act
        tcs.SetResult(MockConnection.Object);

        // Assert
        initializeTask.Should().BeComplete(because: "InitializeAsync should complete after connection factory returns a connection");
        PlayCommand.ExecuteAsync.Should().NotBeNull(because: "the service should have set it");
    }

    [TestMethod]
    public void TiVoService_InitializeAsync_CancelBeforeLocator_ReturnsCancelled()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        Expect_Locator_IsNotCalled();
        Expect_MockConnectionFactory_IsNotCalled();

        CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        Task initializeTask = sut.InitializeAsync(InitializeActivity, cts.Token);

        // Assert
        initializeTask.Should().BeCanceled(because: "the cancellation token was triggred before the locator was called");
    }

    [TestMethod]
    public void TiVoService_InitializeAsync_CancelDuringLocator_ReturnsCancelled()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        CancellationToken cancelled = Expect_Locator_IsCanceled(throwWhenCancelled: true);
        Expect_MockConnectionFactory_IsNotCalled();

        CancellationTokenSource cts = new();

        Task initializeTask = sut.InitializeAsync(InitializeActivity, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        cancelled.IsCancellationRequested.Should().BeTrue(because: "the cancellation token should have been passed through to the Locator");
        initializeTask.Should().BeCanceled(because: "the cancellation token was triggered during the locator call");
    }

    [TestMethod]
    public void TiVoService_InitializeAsync_CancelAfterLocator_ReturnsCancelled()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        MockLocator
            .Setup(x => x.FindTiVoAsync(It.IsAny<CancellationToken>()))
            .Returns(delegate (CancellationToken cancel)
            {
                TaskCompletionSource<EndPoint> tcs = new();
                cancel.Register(() => tcs.SetResult(new MockEndPoint()));
                return tcs.Task;
            });

        Expect_MockConnectionFactory_IsNotCalled();

        CancellationTokenSource cts = new();

        Task initializeTask = sut.InitializeAsync(InitializeActivity, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        initializeTask.Should().BeCanceled(because: "the cancellation token was triggered after the locator completed");
    }

    [TestMethod]
    public void TiVoService_InitializeAsync_CancelDuringConnectionFactory_ReturnsCancelled()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        CancellationToken cancelled = Expect_MockConnectionFactory_IsCanceled(throwWhenCancelled: true);

        CancellationTokenSource cts = new();

        Task initializeTask = sut.InitializeAsync(InitializeActivity, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        cancelled.IsCancellationRequested.Should().BeTrue(because: "the cancellation token should have been passed through to the Connection Factory");
        initializeTask.Should().BeCanceled(because: "the cancellation token was triggered during the connection factory call");
    }

    [TestMethod]
    public void TiVoService_InitializeAsync_CancelAfterConnectionFactory_DisposesConnectionAndReturnsCancelled()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        MockConnectionFactory
            .Setup(x => x.ConnectAsync(It.IsAny<EndPoint>(), It.IsAny<CancellationToken>()))
            .Returns(delegate (EndPoint e, CancellationToken cancel)
            {
                TaskCompletionSource<ITiVoConnection> tcs = new();
                cancel.Register(() => tcs.SetResult(MockConnection.Object));
                return tcs.Task;
            });

        Expect_MockConnection_Disposed();

        CancellationTokenSource cts = new();

        Task initializeTask = sut.InitializeAsync(InitializeActivity, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        initializeTask.Should().BeCanceled(because: "the cancellation token was triggered after the connection factory completed");
    }

    [TestMethod]
    public void TiVoService_CleanUpAsync_WaitsForConnectionDispose()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        MockConnection
            .Setup(x => x.DisposeAsync(It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource().Task)
            .Verifiable(Times.Once);

        // Act
        Task cleanUpTask = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        cleanUpTask.Should().NotBeComplete(because: "the DisposeAsync call returns an incomplete task");

        PlayCommand.IsEnabled.Should().BeFalse(because: "commands should be disabled during cleanup");
    }

    [TestMethod]
    public void TiVoService_CleanUpAsync_CompletesWhenConnectionDisposeAsyncCompletes()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        TaskCompletionSource tcs = new TaskCompletionSource();
        MockConnection
            .Setup(x => x.DisposeAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        Task cleanUpTask = sut.CleanUpAsync(CleanupActivity, default);

        // Act
        tcs.SetResult();

        // Assert
        cleanUpTask.Should().BeComplete(because: "CleanUpAsync should complete after connection DisposeAsync completes");
    }

    [TestMethod]
    public void TiVoService_CleanUpAsync_DoesNothingIfNotInitialized()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        Expect_Locator_IsNotCalled();
        Expect_MockConnectionFactory_IsNotCalled();
        Expect_InitializationActivity_Description_IsNotCalled();

        // Act
        Task cleanUpTask = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        cleanUpTask.Should().BeComplete(because: "CleanUpAsync should complete immediately if service was not initialized");
    }

    [TestMethod]
    public void TiVoService_CleanUpAsync_DoesNothingIfAlreadyDisposed()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        Expect_MockConnection_Disposed();

        _ = sut.CleanUpAsync(CleanupActivity, default);

        // Act
        Task cleanUpTask = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        cleanUpTask.Should().BeComplete(because: "CleanUpAsync should complete immediately if service was already disposed");
    }

    [TestMethod]
    public void TiVoService_CleanUpAsync_DisposeConnection()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        Expect_MockConnection_Disposed();

        // Act
        Task cleanUpTask = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        cleanUpTask.Should().BeComplete(because: "CleanUpAsync should complete after connection DisposeAsync completes");
    }

    [TestMethod]
    public void TiVoService_CleanUpAsync_Cancellation_PassesCancellationToConnectionDispose()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        CancellationToken cancelled = Expect_MockConnection_Disposed_IsCanceled(throwWhenCancelled: false);

        CancellationTokenSource cts = new();

        Task cleanUpTask = sut.CleanUpAsync(CleanupActivity, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        cleanUpTask.Should().NotBeComplete(because: "DisposeAsync did not respond to the cancellation token and is still running");
        cancelled.IsCancellationRequested.Should().BeTrue(because: "the cancellation token should have been passed through to DisposeAsync");
    }

    [TestMethod]
    public void TiVoService_ExecuteAsync_SendsCommandToTiVoConnection()
    {
        // Arrange
        CreateSut();

        Expect_MockConnection_Send(PlayCommand.CommandId);

        // Act
        Task executeTask = PlayCommand.ExecuteAsync!(default);

        // Assert
        executeTask.Should().BeComplete(because: "PlayCommand.ExecuteAsync should return after MockConnection.SendAsync completes");
    }

    [TestMethod]
    public void TiVoService_ExecuteAsync_PassesCancellationTokenToConnection()
    {
        // Arrange
        CreateSut();

        CancellationToken cancelled = Expect_MockConnection_SendAsync_IsCanceled(PlayCommand.CommandId);

        CancellationTokenSource cts = new();
        Task executeTask = PlayCommand.ExecuteAsync!(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        executeTask.Should().NotBeComplete(because: "MockConnection.SendAsync did not respond to the cancellation token and is still running");
        cancelled.IsCancellationRequested.Should().BeTrue(because: "the cancellation token should have been passed through to SendAsync");
    }

    [TestMethod]
    public void TiVoService_ExecuteAsync_WaitsForTiVoConnectionSend()
    {
        // Arrange
        CreateSut();

        MockConnection
            .Setup(x => x.SendIRCommandAsync(PlayCommand.CommandId, It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource().Task)
            .Verifiable(Times.Once);

        // Act
        Task executeTask = PlayCommand.ExecuteAsync!(default);

        // Assert
        executeTask.Should().NotBeComplete(because: "PlayCommand.ExecuteAsync should wait for MockConnection.SendAsync to complete");
    }

    [TestMethod]
    public void TiVoService_ExecuteAsync_CompletesWhenTiVoConnectionSendAsyncCompletes()
    {
        // Arrange
        CreateSut();

        TaskCompletionSource tcs = new TaskCompletionSource();
        MockConnection
            .Setup(x => x.SendIRCommandAsync(PlayCommand.CommandId, It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        Task executeTask = PlayCommand.ExecuteAsync!(default);

        // Act
        tcs.SetResult();

        // Assert
        executeTask.Should().BeComplete(because: "MockConnection.SendAsync completed");
    }

    [TestMethod]
    public void TiVoService_ExecuteAsync_ErrorIfNotInitialized()
    {
        // Arrange
        Expect_Locator_IsNotCalled();
        Expect_MockConnectionFactory_IsNotCalled();
        Expect_InitializationActivity_Description_IsNotCalled();

        CreateUninitializedSut();

        Exception expectedException = Errors.CommandService_NotStarted(PlayCommand);

        // Act
        Task executeTask = PlayCommand.ExecuteAsync!(default);

        // Assert
        executeTask.Should().BeFaultedWith(expectedException,
            because: "the service was not initialized");
    }

    [TestMethod]
    public void TiVoService_ExecuteAsync_ThrowsIfDisposed()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        Exception expectedError = Errors.CommandService_WasShutDown(PlayCommand);

        Expect_MockConnection_Disposed();
        sut.CleanUpAsync(CleanupActivity, default);

        // Act
        Task executeTask = PlayCommand.ExecuteAsync!(default);

        // Assert
        executeTask.Should().BeFaultedWith(expectedError,
            because: "the service was disposed");
    }

    [TestMethod]
    public void TiVoService_ExecuteAsync_ThrowsIfDisposing()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        Exception expectedError = Errors.CommandService_WasShutDown(PlayCommand);

        MockConnection
            .Setup(x => x.DisposeAsync(It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource().Task)
            .Verifiable(Times.Once);

        sut.CleanUpAsync(CleanupActivity, default);

        // Act
        Task executeTask = PlayCommand.ExecuteAsync!(default);

        // Assert
        executeTask.Should().BeFaultedWith(expectedError,
            because: "the service is being disposed");
    }

    private void Expect_MockConnection_Send(string expectedCommand, Task? result = default)
        => MockConnection
            .Setup(x => x.SendIRCommandAsync(expectedCommand, It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(result)
            .Verifiable(Times.Once);
    private CancellationToken Expect_MockConnection_SendAsync_IsCanceled(string expectedCommand)
        => MockConnection
            .Setup(x => x.SendIRCommandAsync(expectedCommand, It.IsAny<CancellationToken>()))
            .WithExpectedCancellation(false);
    private void Expect_MockConnection_Disposed()
        => MockConnection
            .Setup(x => x.DisposeAsync(It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior()
            .Verifiable(Times.Once);
    private CancellationToken Expect_MockConnection_Disposed_IsCanceled(bool throwWhenCancelled)
        => MockConnection
            .Setup(x => x.DisposeAsync(It.IsAny<CancellationToken>()))
            .WithExpectedCancellation(throwWhenCancelled);

    private void Expect_MockConnectionFactory_IsNotCalled()
        => MockConnectionFactory
            .Setup(x => x.ConnectAsync(It.IsAny<EndPoint>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
    private CancellationToken Expect_MockConnectionFactory_IsCanceled(bool throwWhenCancelled)
        => MockConnectionFactory
            .Setup(x => x.ConnectAsync(It.IsAny<EndPoint>(), It.IsAny<CancellationToken>()))
            .WithExpectedCancellation(throwWhenCancelled);

    private void Expect_Locator_IsNotCalled()
        => MockLocator
            .Setup(x => x.FindTiVoAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
    private CancellationToken Expect_Locator_IsCanceled(bool throwWhenCancelled)
        => MockLocator
            .Setup(x => x.FindTiVoAsync(It.IsAny<CancellationToken>()))
            .WithExpectedCancellation(throwWhenCancelled);

    private void Expect_InitializationActivity_Description_IsNotCalled()
        => MockInitializeActivity
            .SetupSet(x => x.Description = "Connecting to TiVo")
            .Verifiable(Times.Never);
}
