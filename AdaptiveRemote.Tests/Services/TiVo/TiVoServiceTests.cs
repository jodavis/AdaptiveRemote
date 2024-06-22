using System.Net;
using AdaptiveRemote.TestUtilities;
using Moq;

namespace AdaptiveRemote.Services.TiVo;

[TestClass]
public class TiVoServiceTests
{
    private readonly Mock<ITiVoLocator> MockLocator = new();
    private readonly Mock<ITiVoConnectionFactory> MockConnectionFactory = new();
    private readonly Mock<ITiVoConnection> MockConnection = new();

    private TiVoService CreateUninitializedSut() => new TiVoService(MockLocator.Object, MockConnectionFactory.Object);
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
                Assert.AreSame(mockEndPoint, ep, "Wrong endpoint was passed to " + nameof(ITiVoConnectionFactory.ConnectAsync));
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
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockLocator.Verify();
        MockConnectionFactory.Verify();
        MockConnection.Verify();
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
    public void TiVoService_InitializeAsync_CreatesTiVoConnection()
    {
        // Arrange
        IScopedLifecycle sut = CreateUninitializedSut();

        // Act
        Task initializeTask = sut.InitializeAsync(default);

        // Assert
        TaskAssert.IsComplete(initializeTask, nameof(initializeTask));
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
    public void TiVoService_SendAsync_SendsCommandToTiVoConnection()
    {
        // Arrange
        const string input = "HELLO";

        ITiVoService sut = CreateSut();

        Expect_MockConnection_SendAsync(input);

        // Act
        Task sendTask = sut.SendAsync(input, default);

        // Assert
        TaskAssert.IsComplete(sendTask, nameof(sendTask));
    }

    [TestMethod]
    public void TiVoService_SendAsync_PassesCancellationTokenToConnection()
    {
        // Arrange
        const string input = "HELLO";

        ITiVoService sut = CreateSut();

        CancellationToken cancelled = Expect_MockConnection_SendAsync_IsCanceled(input);

        CancellationTokenSource cts = new();
        Task sendTask = sut.SendAsync(input, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsNotComplete(sendTask, nameof(sendTask));
        Assert.IsTrue(cancelled.IsCancellationRequested, nameof(cancelled));
    }

    [TestMethod]
    public void TiVoService_SendAsync_WaitsForTiVoConnectionSendAsync()
    {
        // Arrange
        const string input = "HELLO";

        ITiVoService sut = CreateSut();

        MockConnection
            .Setup(x => x.SendIRCommandAsync(input, It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource().Task)
            .Verifiable(Times.Once);

        // Act
        Task sendTask = sut.SendAsync(input, default);

        // Assert
        TaskAssert.IsNotComplete(sendTask, nameof(sendTask));
    }

    [TestMethod]
    public void TiVoService_SendAsync_CompletesWhenTiVoConnectionSendAsyncCompletes()
    {
        // Arrange
        const string input = "HELLO";

        ITiVoService sut = CreateSut();

        TaskCompletionSource tcs = new TaskCompletionSource();
        MockConnection
            .Setup(x => x.SendIRCommandAsync(input, It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        Task sendTask = sut.SendAsync(input, default);

        // Act
        tcs.SetResult();

        // Assert
        TaskAssert.IsComplete(sendTask, nameof(sendTask));
    }

    [TestMethod]
    public void TiVoService_SendAsync_ThrowsIfNotInitialized()
    {
        // Arrange
        const string input = "HELLO";

        ITiVoService sut = CreateUninitializedSut();

        Expect_Locator_IsNotCalled();
        Expect_MockConnectionFactory_IsNotCalled();

        Exception expectedError = Models.Errors.TiVo_NotInitialized(input);

        // Act
        Task sendTask = sut.SendAsync(input, default);

        // Assert
        TaskAssert.IsFaulted(sendTask, expectedError, nameof(sendTask));
    }

    [TestMethod]
    public void TiVoService_SendAsync_ThrowsIfDisposed()
    {
        // Arrange
        const string input = "HELLO";

        ITiVoService sut = CreateSut();

        Exception expectedError = Models.Errors.TiVo_NotInitialized(input);

        Expect_MockConnection_Disposed();
        ((IScopedLifecycle)sut).CleanUpAsync(default);

        // Act
        Task sendTask = sut.SendAsync(input, default);

        // Assert
        TaskAssert.IsFaulted(sendTask, expectedError, nameof(sendTask));
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
