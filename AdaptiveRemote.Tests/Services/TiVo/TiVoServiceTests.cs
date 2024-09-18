using System.Net;
using AdaptiveRemote.Models;
using Moq;

namespace AdaptiveRemote.Services.TiVo;

[TestClass]
public class TiVoServiceTests
{
    private readonly Mock<ITiVoLocator> MockLocator = new();
    private readonly Mock<ITiVoConnection.Factory> MockConnectionFactory = new();
    private readonly Mock<ITiVoConnection> MockConnection = new();
    private readonly Mock<IRemoteDefinitionService> MockDefinition = new();
    private readonly MockLogger<TiVoService> MockLogger = new();

    private readonly LayoutGroup Commands = new("ROOT",
        [
            new LifecycleCommand("UNUSED1"),
            new TiVoCommand("Play"),
            new TiVoCommand("Stop"),
            new LifecycleCommand("UNUSED2"),
        ]);
    private TiVoCommand PlayCommand => Commands.Elements.OfType<TiVoCommand>().First();

    private TiVoService CreateUninitializedSut() => new(MockLocator.Object, MockConnectionFactory.Object, MockDefinition.Object, MockLogger);
    private TiVoService CreateSut()
    {
        const int InitializeTimeoutInMilliseconds = 1000;
        TiVoService sut = CreateUninitializedSut();
        Assert.IsTrue(((IScopedLifecycle)sut).InitializeAsync(default).Wait(InitializeTimeoutInMilliseconds), nameof(TiVoService) + " did not initialize within {0}ms", InitializeTimeoutInMilliseconds);
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
                Assert.AreSame(mockEndPoint, ep, "Wrong endpoint was passed to " + nameof(ITiVoConnection.Factory.ConnectAsync));
            })
            .Returns(Task.FromResult(MockConnection.Object))
            .Verifiable(Times.Once);

        MockConnection
            .Setup(x => x.SendIRCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(delegate (string commandId, CancellationToken cancellation)
            {
                Assert.Fail("Did not expect {0}.{1}(\"{2}\")",
                    nameof(ITiVoConnection),
                    nameof(ITiVoConnection.SendIRCommandAsync),
                    commandId);
            });
        MockConnection
            .Setup(x => x.DisposeAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);

        MockDefinition
            .SetupGet(x => x.RemoteRoot)
            .Returns(Commands)
            .Verifiable(Times.Once);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockLocator.Verify();
        MockConnectionFactory.Verify();
        MockConnection.Verify();
        MockDefinition.Verify();
    }

    [TestMethod]
    public void TiVoService_Constructor_DoesNotCreateTiVoConnection()
    {
        // Arrange
        Expect_Locator_IsNotCalled();
        Expect_MockConnectionFactory_IsNotCalled();

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

        IScopedLifecycle sut = CreateUninitializedSut();

        // Act
        string result = sut.Name;

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(result), nameof(result) + ".IsNullOrEmpty");
    }

    [TestMethod]
    public void TiVoService_InitializeAsync_CreatesTiVoConnectionAndEnablesCommands()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        // Act
        Task initializeTask = sut.InitializeAsync(default);

        // Assert
        TaskAssert.IsComplete(initializeTask, nameof(initializeTask));

        foreach (TiVoCommand command in Commands.Elements.OfType<TiVoCommand>())
        {
            Assert.IsNotNull(command.ExecuteAsync, "Service did not set {0} on {1} command",
                nameof(command.ExecuteAsync),
                command.Name);
            Assert.IsTrue(command.IsEnabled, "Service did not set {0} on {1} command",
                nameof(command.IsEnabled),
                command.Name);
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
        Task initializeTask = sut.InitializeAsync(default);

        // Assert
        TaskAssert.IsNotComplete(initializeTask, nameof(initializeTask));
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

        Task initializeTask = sut.InitializeAsync(default);

        // Act
        tcs.SetResult(expectedEndPoint);

        // Assert
        TaskAssert.IsComplete(initializeTask, nameof(initializeTask));
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
        Task initializeTask = sut.InitializeAsync(default);

        // Assert
        TaskAssert.IsNotComplete(initializeTask, nameof(initializeTask));
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

        Task initializeTask = sut.InitializeAsync(default);

        // Act
        tcs.SetResult(MockConnection.Object);

        // Assert
        TaskAssert.IsComplete(initializeTask, nameof(initializeTask));
        Assert.IsNotNull(PlayCommand.ExecuteAsync, "Service did not set ExecuteAsync on PlayCommand");
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
        Task initializeTask = sut.InitializeAsync(cts.Token);

        // Assert
        TaskAssert.IsCanceled(initializeTask, nameof(initializeTask));
    }

    [TestMethod]
    public void TiVoService_InitializeAsync_CancelDuringLocator_ReturnsCancelled()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        CancellationToken cancelled = Expect_Locator_IsCanceled(throwWhenCancelled: true);
        Expect_MockConnectionFactory_IsNotCalled();

        CancellationTokenSource cts = new();

        Task initializeTask = sut.InitializeAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        Assert.IsTrue(cancelled.IsCancellationRequested, nameof(cancelled));
        TaskAssert.IsCanceled(initializeTask, nameof(initializeTask));
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

        Task initializeTask = sut.InitializeAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsCanceled(initializeTask, nameof(initializeTask));
    }

    [TestMethod]
    public void TiVoService_InitializeAsync_CancelDuringConnectionFactory_ReturnsCancelled()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        CancellationToken cancelled = Expect_MockConnectionFactory_IsCanceled(throwWhenCancelled: true);

        CancellationTokenSource cts = new();

        Task initializeTask = sut.InitializeAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        Assert.IsTrue(cancelled.IsCancellationRequested, nameof(cancelled));
        TaskAssert.IsCanceled(initializeTask, nameof(initializeTask));
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

        Task initializeTask = sut.InitializeAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsCanceled(initializeTask, nameof(initializeTask));
    }

    [TestMethod]
    public void TiVoService_CleanUpAsync_WaitsForConnectionDisposeAsync()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        MockConnection
            .Setup(x => x.DisposeAsync(It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource().Task)
            .Verifiable(Times.Once);

        // Act
        Task cleanUpTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsNotComplete(cleanUpTask, nameof(cleanUpTask));

        Assert.IsFalse(PlayCommand.IsEnabled, nameof(PlayCommand.IsEnabled));
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

        Task cleanUpTask = sut.CleanUpAsync(default);

        // Act
        tcs.SetResult();

        // Assert
        TaskAssert.IsComplete(cleanUpTask, nameof(cleanUpTask));
    }

    [TestMethod]
    public void TiVoService_CleanUpAsync_DoesNothingIfNotInitialized()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        Expect_Locator_IsNotCalled();
        Expect_MockConnectionFactory_IsNotCalled();

        // Act
        Task cleanUpTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsComplete(cleanUpTask, nameof(cleanUpTask));
    }

    [TestMethod]
    public void TiVoService_CleanUpAsync_DoesNothingIfAlreadyDisposed()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        Expect_MockConnection_Disposed();

        _ = sut.CleanUpAsync(default);

        // Act
        Task cleanUpTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsComplete(cleanUpTask, nameof(cleanUpTask));
    }

    [TestMethod]
    public void TiVoService_CleanUpAsync_DisposeConnection()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        Expect_MockConnection_Disposed();

        // Act
        Task cleanUpTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsComplete(cleanUpTask, nameof(cleanUpTask));
    }

    [TestMethod]
    public void TiVoService_CleanUpAsync_Cancellation_PassesCancellationToConnectionDisposeAsync()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        CancellationToken cancelled = Expect_MockConnection_Disposed_IsCanceled(throwWhenCancelled: false);

        CancellationTokenSource cts = new();

        Task cleanUpTask = sut.CleanUpAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsNotComplete(cleanUpTask, nameof(cleanUpTask));
        Assert.IsTrue(cancelled.IsCancellationRequested, nameof(cancelled.IsCancellationRequested));
    }

    [TestMethod]
    public void TiVoService_ExecuteAsync_SendsCommandToTiVoConnection()
    {
        // Arrange
        CreateSut();

        Expect_MockConnection_SendAsync(PlayCommand.CommandId);

        // Act
        Task executeTask = PlayCommand.ExecuteAsync!(default);

        // Assert
        TaskAssert.IsComplete(executeTask, nameof(executeTask));
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
        TaskAssert.IsNotComplete(executeTask, nameof(executeTask));
        Assert.IsTrue(cancelled.IsCancellationRequested, nameof(cancelled));
    }

    [TestMethod]
    public void TiVoService_ExecuteAsync_WaitsForTiVoConnectionSendAsync()
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
        TaskAssert.IsNotComplete(executeTask, nameof(executeTask));
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
        TaskAssert.IsComplete(executeTask, nameof(executeTask));
    }

    [TestMethod]
    public void TiVoService_ExecuteAsync_ErrorIfNotInitialized()
    {
        // Arrange
        Expect_Locator_IsNotCalled();
        Expect_MockConnectionFactory_IsNotCalled();

        CreateUninitializedSut();

        Exception expectedException = Errors.CommandService_NotStarted(PlayCommand);

        // Act
        Task executeTask = PlayCommand.ExecuteAsync!(default);

        // Assert
        TaskAssert.IsFaulted(executeTask, expectedException, nameof(executeTask));
    }

    [TestMethod]
    public void TiVoService_ExecuteAsync_ThrowsIfDisposed()
    {
        // Arrange
        IScopedLifecycle sut = CreateSut();

        Exception expectedError = Errors.CommandService_WasShutDown(PlayCommand);

        Expect_MockConnection_Disposed();
        sut.CleanUpAsync(default);

        // Act
        Task executeTask = PlayCommand.ExecuteAsync!(default);

        // Assert
        TaskAssert.IsFaulted(executeTask, expectedError, nameof(executeTask));
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

        sut.CleanUpAsync(default);

        // Act
        Task executeTask = PlayCommand.ExecuteAsync!(default);

        // Assert
        TaskAssert.IsFaulted(executeTask, expectedError, nameof(executeTask));
    }

    private void Expect_MockConnection_SendAsync(string expectedCommand, Task? result = default)
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
}
