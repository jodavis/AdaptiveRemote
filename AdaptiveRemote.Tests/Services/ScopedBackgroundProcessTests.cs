using AdaptiveRemote.Logging;
using Moq;

namespace AdaptiveRemote.Services;

[TestClass]
public class ScopedBackgroundProcessTests
{
    private readonly Mock<ILifecycleActivity> MockInitializeActivity = new() { Name = nameof(MockInitializeActivity) };
    private readonly Mock<ILifecycleActivity> MockCleanupActivity = new() { Name = nameof(MockCleanupActivity) };

    private ILifecycleActivity InitializeActivity => MockInitializeActivity.Object;
    private ILifecycleActivity CleanupActivity => MockCleanupActivity.Object;

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
        Task resultTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        resultTask.Should().BeComplete(because: "InitializeAsync should complete after ExecuteAsync is started");
        sut.ExecuteTask.Should().NotBeComplete(because: "ExecuteAsync should be running after InitializeAsync completes");

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
        Task resultTask = sut.InitializeAsync(InitializeActivity, cancelled.Token);

        // Assert
        resultTask.Should().BeCanceled(because: "InitializeAsync was cancelled by the CancellationToken");
        sut.ExecuteTask.Should().BeCanceled(because: "CancellationToken from InitializeAsync was passed to ExecuteAsync");

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
        Task resultTask = sut.InitializeAsync(InitializeActivity, cts.Token);

        // Assert
        sut.ExecuteTask.Should().BeCanceled(because: "the CancellationToken was set after ExecuteAsync started but before moving to the worker thread.");
        resultTask.Should().BeCanceled(because: "InitializeAsync's cancellation token was triggered");

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
        Task resultTask = sut.InitializeAsync(InitializeActivity, cts.Token);

        // Assert
        sut.ExecuteTask.Should().BeCanceled(because: "the CancellationToken was set after moving to the worker thread but before ExecuteAsync started.");
        resultTask.Should().BeCanceled(because: "InitializeAsync's cancellation token was triggered");

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
        Task resultTask = sut.InitializeAsync(InitializeActivity, cts.Token);

        // Assert
        sut.ExecuteTask.Should().BeCanceled(because: "InitializeAsync's cancellation token was triggered");
        resultTask.Should().BeCanceled(because: "InitializeAsync's cancellation token was triggered");

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

        Task initializeTask = sut.InitializeAsync(InitializeActivity, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        sut.ExecuteTask.Should().NotBeComplete(because: "ExecuteAsync should still be running after InitializeAsync completes");
        initializeTask.Should().BeComplete(because: "the service initialized successfully");

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
        Task resultTask = sut.InitializeAsync(InitializeActivity, default);

        // Assert
        resultTask.Should().BeFaultedWith(expectedException,
            because: "InitializeAsync should fail if ExecuteAsync fails during initialization");
        sut.ExecuteTask.Should().BeFaultedWith(expectedException,
            because: "ExecuteAsync should fail with the same exception");

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

        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        // Act
        sut.ExecuteCompletionSource.SetException(expectedException);

        // Assert
        initializeTask.Should().BeComplete(because: "the exception happened after initialization completed");
        sut.ExecuteTask.Should().BeFaultedWith(expectedException,
            because: "ExecuteAsync threw an exception");

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

        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        // Act
        sut.ExecuteCompletionSource.SetException(expectedException);

        // Assert
        initializeTask.Should().BeComplete(because: "the cancellation happened after initialization completed");
        sut.ExecuteTask.Should().BeCanceled(because: "ExecuteAsync threw a TaskCancelledException");

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

        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        // Act
        sut.ExecuteCompletionSource.SetResult();

        // Assert
        initializeTask.Should().BeComplete(because: "initialization completed successfully");
        sut.ExecuteTask.Should().BeComplete(because: "ExecuteAsync ran to completion");

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

        Task initializeTask = sut.InitializeAsync(InitializeActivity, default);

        // Act
        sut.ExecuteCompletionSource.SetException(expectedException);

        // Assert
        initializeTask.Should().BeComplete(because: "initialization completed successfully");
        sut.ExecuteTask.Should().BeCanceled(because: "ExecuteAsync threw a TaskCanceledException");

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

        sut.InitializeAsync(InitializeActivity, default)
            .Should().BeComplete(because: "the service should initialize synchronously");

        // Act
        Task resultTask = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        resultTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1),
            because: "CleanUpAsync should complete after ExecuteAsync ends");
        sut.ExecuteTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1),
            because: "ExecuteAsync should run to completion after the stop token is set");

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

        sut.InitializeAsync(InitializeActivity, default)
            .Should().BeComplete(because: "InitializeAsync should complete synchronously");

        // Act
        Task resultTask = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        resultTask.Should().NotBeCompleteWithin(TimeSpan.FromSeconds(1),
            because: "this ExecuteAsync does not respond to the stop token");
        sut.ExecuteTask.Should().NotBeCompleteWithin(TimeSpan.FromSeconds(1),
            because: "this ExecuteAsync does not respond to the stop token");

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

        sut.InitializeAsync(InitializeActivity, default)
            .Should().BeComplete(because: "InitializeAsync should complete synchronously");

        Task resultTask = sut.CleanUpAsync(CleanupActivity, default);

        // Act
        sut.ExecuteCompletionSource.SetException(expectedError);

        // Assert
        resultTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1),
            because: "CleanUpAsync should complete even if ExecuteAsync faults");

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
        Task resultTask = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        resultTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1),
            because: "CleanUpAsync should be a no-op if the service was never started");

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

        sut.InitializeAsync(InitializeActivity, default)
            .Should().BeComplete(because: "InitializeAsync should complete synchronously");

        sut.ExecuteCompletionSource.SetResult();
        sut.ExecuteTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1),
            because: "result was set on ExecuteCompletionSource");

        // Act
        Task resultTask = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        resultTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1),
            because: "CleanUpAsync should be a no-op if ExecuteAsync has already run to completion");

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

        sut.InitializeAsync(InitializeActivity, default)
            .Should().BeComplete(because: "InitializeAsync should complete synchronously");
        CancellationTokenSource cts = new();
        cts.Cancel();
        sut.ExecuteCompletionSource.SetCanceled(cts.Token);

        sut.ExecuteTask.Should().BeCanceled(because: "ExecuteCompletionSource was cancelled");

        // Act
        Task resultTask = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        resultTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1),
            because: "CleanUpAsync should be a no-op if ExecuteAsync was already cancelled");

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

        sut.InitializeAsync(InitializeActivity, default)
            .Should().BeComplete(because: "InitializeAsync should complete sychronously");
        sut.ExecuteCompletionSource.SetException(expectedException);

        sut.ExecuteTask.Should().BeFaultedWith(expectedException,
            because: "ExecuteCompletionSource.SetException was called");

        // Act
        Task resultTask = sut.CleanUpAsync(CleanupActivity, default);

        // Assert
        resultTask.Should().BeComplete(because: "CleanUpAsync should be a no-op if ExecuteAsync has already faulted");

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
                    _initializeTask.Should().NotBeNull(because: "ExecuteAsync should not be called until InitializeAsync has started");
                    _initializeTask.Should().NotBeComplete(because: "The InitializeAsync should not complete until ExecuteAsync completes or awaits something");
                    ExecuteAsyncCallback?.Invoke();
                })
                .Verifiable(Times.Once);

            _mockMethods
                .Setup(x => x.MoveToWorkerThreadAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<Task> task, CancellationToken cancellationToken) =>
                {
                    _initializeTask.Should().BeNull(because: "it can't be set until it awaits the call to " + nameof(MoveToWorkerThreadAsync));
                    BeforeMoveToWorkerThreadAsyncCallback?.Invoke();
                    await _moveToWorkerThreadTcs.Task;

                    cancellationToken.ThrowIfCancellationRequested();

                    AfterMoveToWorkerThreadAsyncCallback?.Invoke();
                    await task();
                })
                .Verifiable(Times.Once);
        }

        public override Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
        {
            _initializeTask = base.InitializeAsync(activity, cancellationToken);
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
