using AdaptiveRemote.Logging;
using AdaptiveRemote.TestUtilities;
using Moq;

namespace AdaptiveRemote.Services;

[TestClass]
public class ScopedBackgroundProcessTests
{
    private static TestBackgroundProcess CreateSut() => new();

    private static string Expect_Starting
        => $"Information[1201]: {LoggingMessages.ScopedBackgroundProcess_Starting}";
    private static string Expect_Started
        => $"Information[1202]: {LoggingMessages.ScopedBackgroundProcess_Started}";
    private static string Expect_Stopping
        => $"Information[1203]: {LoggingMessages.ScopedBackgroundProcess_Stopping}";
    private static string Expect_Stopped
        => $"Information[1204]: {LoggingMessages.ScopedBackgroundProcess_Stopped}";
    private static string Expect_StoppedEarly
        => $"Warning[1205]: {LoggingMessages.ScopedBackgroundProcess_StoppedEarly}";
    private static string Expect_Error(Exception error)
        => $"Error[1206]: {string.Format(LoggingMessages.ScopedBackgroundProcess_Error, error)}";
    private static string Expect_CanceledBeforeStarted
        => $"Warning[1207]: {LoggingMessages.ScopedBackgroundProcess_CanceledBeforeStarted}";

    [TestMethod]
    public void ScopedBackgroundProcess_InitializeAsync_CallsExecuteAsync()
    {
        // Arrange
        TestBackgroundProcess sut = CreateSut()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_Started
            );

        // Act
        Task resultTask = sut.InitializeAsync(default);

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));
        TaskAssert.IsNotComplete(sut.ExecuteTask, nameof(sut.ExecuteTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_InitializeAsync_CancelledBeforeStart_DoesNothing()
    {
        // Arrange
        TestBackgroundProcess sut = CreateSut()
            .Expect_ExecuteAsync_IsNotCalled()
            .Expect_MoveToWorkerThreadAsync_IsNotCalled()
            .Expect_LogMessages(
                Expect_CanceledBeforeStarted
            );

        CancellationTokenSource cancelled = new();
        cancelled.Cancel();

        // Act
        Task resultTask = sut.InitializeAsync(cancelled.Token);

        // Assert
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));
        TaskAssert.IsCanceled(sut.ExecuteTask, nameof(sut.ExecuteTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_InitializeAsync_CancelledBeforeMoveToWorkerThread_LogsCancellation()
    {
        // Arrange
        TestBackgroundProcess sut = CreateSut()
            .Expect_ExecuteAsync_IsNotCalled()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_CanceledBeforeStarted);

        CancellationTokenSource cts = new();
        sut.BeforeMoveToWorkerThreadAsyncCallback = cts.Cancel;

        // Act
        Task resultTask = sut.InitializeAsync(cts.Token);

        // Assert
        TaskAssert.IsCanceled(sut.ExecuteTask, nameof(sut.ExecuteTask));
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_InitializeAsync_CancelledBeforeExecuteAsync_LogsCancellation()
    {
        // Arrange
        TestBackgroundProcess sut = CreateSut()
            .Expect_ExecuteAsync_IsNotCalled()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_CanceledBeforeStarted);

        CancellationTokenSource cts = new();
        sut.AfterMoveToWorkerThreadAsyncCallback = cts.Cancel;

        // Act
        Task resultTask = sut.InitializeAsync(cts.Token);

        // Assert
        TaskAssert.IsCanceled(sut.ExecuteTask, nameof(sut.ExecuteTask));
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_InitializeAsync_CancelledDuringExecuteAsync_CancelsExecuteAsyncAndLogsCancellation()
    {
        // Arrange
        TestBackgroundProcess sut = CreateSut()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_CanceledBeforeStarted);

        CancellationTokenSource cts = new();
        sut.ExecuteAsyncCallback = cts.Cancel;

        // Act
        Task resultTask = sut.InitializeAsync(cts.Token);

        // Assert
        TaskAssert.IsCanceled(sut.ExecuteTask, nameof(sut.ExecuteTask));
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_InitializeAsync_CancelledAfterInitialized_DoesNothing()
    {
        // Arrange
        TestBackgroundProcess sut = CreateSut()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_Started);

        CancellationTokenSource cts = new();

        Task initializeTask = sut.InitializeAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsNotComplete(sut.ExecuteTask, nameof(sut.ExecuteTask));
        TaskAssert.IsComplete(initializeTask, nameof(initializeTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_InitializeAsync_OnErrorDuringInitialize_ReturnsFailedTask()
    {
        // Arrange
        Exception expectedException = new DataMisalignedException();

        TestBackgroundProcess sut = CreateSut()
            .Expect_LogMessages(
                Expect_Starting
            // No error logged here because lifecycle should report it
            );

        sut.ExecuteCompletionSource.SetException(expectedException);

        // Act
        Task resultTask = sut.InitializeAsync(default);

        // Assert
        TaskAssert.IsFaulted(resultTask, expectedException, nameof(resultTask));
        TaskAssert.IsFaulted(sut.ExecuteTask, expectedException, nameof(sut.ExecuteTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_ExecuteAsync_OnError_LogsError()
    {
        // Arrange
        Exception expectedException = new DataMisalignedException();

        TestBackgroundProcess sut = CreateSut()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_Started,
                Expect_Error(expectedException)
            );

        Task initializeTask = sut.InitializeAsync(default);

        // Act
        sut.ExecuteCompletionSource.SetException(expectedException);

        // Assert
        TaskAssert.IsComplete(initializeTask, nameof(initializeTask));
        TaskAssert.IsFaulted(sut.ExecuteTask, expectedException, nameof(sut.ExecuteTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_ExecuteAsync_OnCancelled_LogsStoppedEarly()
    {
        // Arrange
        Exception expectedException = new TaskCanceledException();

        TestBackgroundProcess sut = CreateSut()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_Started,
                Expect_StoppedEarly
            );

        Task initializeTask = sut.InitializeAsync(default);

        // Act
        sut.ExecuteCompletionSource.SetException(expectedException);

        // Assert
        TaskAssert.IsComplete(initializeTask, nameof(initializeTask));
        TaskAssert.IsCanceled(sut.ExecuteTask, nameof(sut.ExecuteTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_ExecuteAsync_OnComplete_LogsStoppedEarly()
    {
        // Arrange
        TestBackgroundProcess sut = CreateSut()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_Started,
                Expect_StoppedEarly
            );

        Task initializeTask = sut.InitializeAsync(default);

        // Act
        sut.ExecuteCompletionSource.SetResult();

        // Assert
        TaskAssert.IsComplete(initializeTask, nameof(initializeTask));
        TaskAssert.IsComplete(sut.ExecuteTask, nameof(sut.ExecuteTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_ExecuteAsync_OnTaskCompleted_LogsStoppedEarly()
    {
        // Arrange
        Exception expectedException = new TaskCanceledException();

        TestBackgroundProcess sut = CreateSut()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_Started,
                Expect_StoppedEarly
            );

        Task initializeTask = sut.InitializeAsync(default);

        // Act
        sut.ExecuteCompletionSource.SetException(expectedException);

        // Assert
        TaskAssert.IsComplete(initializeTask, nameof(initializeTask));
        TaskAssert.IsCanceled(sut.ExecuteTask, nameof(sut.ExecuteTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_CleanUpAsync_CancelsExecuteAsync()
    {
        // Arrange
        TestBackgroundProcess sut = CreateSut()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_Started,
                Expect_Stopping,
                Expect_Stopped
            );

        TaskAssert.IsComplete(sut.InitializeAsync(default), nameof(sut.InitializeAsync));

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsComplete(resultTask, TimeSpan.FromSeconds(1), nameof(resultTask));
        TaskAssert.IsComplete(sut.ExecuteTask, TimeSpan.FromSeconds(1), nameof(sut.ExecuteTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_CleanUpAsync_WaitsForExecuteAsyncToComplete()
    {
        // Arrange
        TestBackgroundProcess sut = CreateSut()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_Started,
                Expect_Stopping
            )
            .Expect_ExecuteAsync_DoesNotComplete();

        TaskAssert.IsComplete(sut.InitializeAsync(default), nameof(sut.InitializeAsync));

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsNotComplete(resultTask, TimeSpan.FromSeconds(1), nameof(resultTask));
        TaskAssert.IsNotComplete(sut.ExecuteTask, TimeSpan.FromSeconds(1), nameof(sut.ExecuteTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_CleanUpAsync_OnExecuteAsyncError_ReturnsCompletedTask()
    {
        // Arrange
        Exception expectedError = new("CleanupAsync shouldn't see this exception");

        TestBackgroundProcess sut = CreateSut()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_Started,
                Expect_Stopping,
                Expect_Error(expectedError),
                Expect_Stopped
            )
            .Expect_ExecuteAsync_IgnoresCancellationToken();

        TaskAssert.IsComplete(sut.InitializeAsync(default), nameof(sut.InitializeAsync));

        Task resultTask = sut.CleanUpAsync(default);

        // Act
        sut.ExecuteCompletionSource.SetException(expectedError);

        // Assert
        TaskAssert.IsComplete(resultTask, TimeSpan.FromSeconds(1), nameof(resultTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_CleanUpAsync_WhenNotStarted_DoesNothing()
    {
        // Arrange
        TestBackgroundProcess sut = CreateSut()
            .Expect_ExecuteAsync_IsNotCalled()
            .Expect_MoveToWorkerThreadAsync_IsNotCalled()
            .Expect_ExecuteAsync_IgnoresCancellationToken();

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsComplete(resultTask, TimeSpan.FromSeconds(1), nameof(resultTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_CleanUpAsync_WhenAlreadyComplete_DoesNothing()
    {
        // Arrange
        TestBackgroundProcess sut = CreateSut()
            .Expect_ExecuteAsync_IgnoresCancellationToken()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_Started,
                Expect_StoppedEarly
            );

        TaskAssert.IsComplete(sut.InitializeAsync(default), nameof(sut.InitializeAsync));
        sut.ExecuteCompletionSource.SetResult();

        TaskAssert.IsComplete(sut.ExecuteTask!, TimeSpan.FromSeconds(1), nameof(sut.ExecuteTask));

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsComplete(resultTask, TimeSpan.FromSeconds(1), nameof(resultTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_CleanUpAsync_WhenCancelled_DoesNothing()
    {
        // Arrange
        TestBackgroundProcess sut = CreateSut()
            .Expect_ExecuteAsync_IgnoresCancellationToken()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_Started,
                Expect_StoppedEarly
            );

        TaskAssert.IsComplete(sut.InitializeAsync(default), nameof(sut.InitializeAsync));
        CancellationTokenSource cts = new();
        cts.Cancel();
        sut.ExecuteCompletionSource.SetCanceled(cts.Token);

        TaskAssert.IsCanceled(sut.ExecuteTask!, TimeSpan.FromSeconds(1), nameof(sut.ExecuteTask));

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsComplete(resultTask, TimeSpan.FromSeconds(1), nameof(resultTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    [TestMethod]
    public void ScopedBackgroundProcess_CleanUpAsync_WhenAlreadyFaulted_DoesNothing()
    {
        // Arrange
        Exception expectedException = new("CleanUpAsync shouldn't see this exception");

        TestBackgroundProcess sut = CreateSut()
            .Expect_ExecuteAsync_IgnoresCancellationToken()
            .Expect_LogMessages(
                Expect_Starting,
                Expect_Started,
                Expect_Error(expectedException)
            );

        TaskAssert.IsComplete(sut.InitializeAsync(default), nameof(sut.InitializeAsync));
        sut.ExecuteCompletionSource.SetException(expectedException);

        TaskAssert.IsFaulted(sut.ExecuteTask!, expectedException, TimeSpan.FromSeconds(1), nameof(sut.ExecuteTask));

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsComplete(resultTask, TimeSpan.FromSeconds(1), nameof(resultTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    private class TestBackgroundProcess : ScopedBackgroundProcess
    {
        private readonly Mock<IMockMethods> _mockMethods = new();
        private readonly List<string> _expectedLogMessages = new();
        private readonly TaskCompletionSource _moveToWorkerThreadTcs = new();
        private Task? _initializeTask = null;

        public TaskCompletionSource ExecuteCompletionSource { get; } = new();

        public Action? BeforeMoveToWorkerThreadAsyncCallback { get; set; }
        public Action? AfterMoveToWorkerThreadAsyncCallback { get; set; }
        public Action? ExecuteAsyncCallback { get; set; }

        public TestBackgroundProcess()
            : base(nameof(TestBackgroundProcess), new MockLogger<TestBackgroundProcess>())
        {
            _mockMethods
                .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
                .WithStandardTaskBehavior(ExecuteCompletionSource.Task)
                .Callback(() =>
                {
                    Assert.IsNotNull(_initializeTask, nameof(_initializeTask) + " during " + nameof(ExecuteAsync));
                    TaskAssert.IsNotComplete(_initializeTask, nameof(_initializeTask) + " during " + nameof(ExecuteAsync));
                    ExecuteAsyncCallback?.Invoke();
                })
                .Verifiable(Times.Once);

            _mockMethods
                .Setup(x => x.MoveToWorkerThreadAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<Task> task, CancellationToken cancellationToken) =>
                {
                    Assert.IsNull(_initializeTask, nameof(_initializeTask) + " during " + nameof(MoveToWorkerThreadAsync));
                    BeforeMoveToWorkerThreadAsyncCallback?.Invoke();
                    await _moveToWorkerThreadTcs.Task;

                    cancellationToken.ThrowIfCancellationRequested();

                    AfterMoveToWorkerThreadAsyncCallback?.Invoke();
                    await task();
                })
                .Verifiable(Times.Once);
        }

        public override Task InitializeAsync(CancellationToken cancellationToken)
        {
            _initializeTask = base.InitializeAsync(cancellationToken);
            _moveToWorkerThreadTcs.TrySetResult();
            return _initializeTask;
        }

        protected override Task ExecuteAsync(CancellationToken stopToken)
            => _mockMethods.Object.ExecuteAsync(stopToken);
        protected override Task MoveToWorkerThreadAsync(Func<Task> task, CancellationToken cancellationToken)
            => _mockMethods.Object.MoveToWorkerThreadAsync(task, cancellationToken);

        public void VerifyMethodCalls() => _mockMethods.Verify();
        public void VerifyLogMessages() => ((MockLogger<TestBackgroundProcess>)Logger).VerifyMessages(_expectedLogMessages.ToArray());

        public TestBackgroundProcess Expect_ExecuteAsync_IsNotCalled()
        {
            _mockMethods
                .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
                .Verifiable(Times.Never);
            return this;
        }

        public TestBackgroundProcess Expect_MoveToWorkerThreadAsync_IsNotCalled()
        {
            _mockMethods
                .Setup(x => x.MoveToWorkerThreadAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Verifiable(Times.Never);
            return this;
        }

        internal TestBackgroundProcess Expect_LogMessages(params string[] expectedMessages)
        {
            _expectedLogMessages.AddRange(expectedMessages);
            return this;
        }

        internal TestBackgroundProcess Expect_ExecuteAsync_DoesNotComplete()
        {
            _mockMethods
                .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
                .Returns(new TaskCompletionSource().Task);
            return this;
        }

        internal TestBackgroundProcess Expect_ExecuteAsync_IgnoresCancellationToken()
        {
            _mockMethods
                .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
                .Returns(ExecuteCompletionSource.Task);
            return this;
        }
    }

    public interface IMockMethods
    {
        Task ExecuteAsync(CancellationToken stopToken);

        Task MoveToWorkerThreadAsync(Func<Task> task, CancellationToken cancellationToken);
    }
}
