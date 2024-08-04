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
    public void ScopedBackgroundProcess_InitializeAsync_OnError_ReturnsFailedTask()
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
            .Expect_ExecuteAsyncDoesNotComplete();

        TaskAssert.IsComplete(sut.InitializeAsync(default), nameof(sut.InitializeAsync));

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsNotComplete(resultTask, TimeSpan.FromSeconds(1), nameof(resultTask));
        TaskAssert.IsNotComplete(sut.ExecuteTask, TimeSpan.FromSeconds(1), nameof(sut.ExecuteTask));

        sut.VerifyMethodCalls();
        sut.VerifyLogMessages();
    }

    private class TestBackgroundProcess : ScopedBackgroundProcess
    {
        private readonly Mock<IMockMethods> _mockMethods = new();
        private readonly List<string> _expectedLogMessages = new();
        private readonly TaskCompletionSource _executeOnThreadTcs = new();
        private Task? _initializeTask = null;

        public TaskCompletionSource ExecuteCompletionSource { get; } = new();

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
                })
                .Verifiable(Times.Once);

            _mockMethods
                .Setup(x => x.ExecuteOnThreadAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<CancellationToken, Task> task, CancellationToken cancellationToken) =>
                {
                    Assert.IsNull(_initializeTask, nameof(_initializeTask) + " during " + nameof(ExecuteOnThreadAsync));
                    await _executeOnThreadTcs.Task;
                    await task(cancellationToken);
                })
                .Verifiable(Times.Once);
        }

        public override Task InitializeAsync(CancellationToken cancellationToken)
        {
            _initializeTask = base.InitializeAsync(cancellationToken);
            _executeOnThreadTcs.SetResult();
            return _initializeTask;
        }

        protected override Task ExecuteAsync(CancellationToken stopToken)
            => _mockMethods.Object.ExecuteAsync(stopToken);
        protected override Task ExecuteOnThreadAsync(Func<CancellationToken, Task> task, CancellationToken cancellationToken)
            => _mockMethods.Object.ExecuteOnThreadAsync(task, cancellationToken);

        public void VerifyMethodCalls() => _mockMethods.Verify();
        public void VerifyLogMessages() => ((MockLogger<TestBackgroundProcess>)Logger).VerifyMessages(_expectedLogMessages.ToArray());

        public TestBackgroundProcess Expect_ExecuteAsync_IsNotCalled()
        {
            _mockMethods
                .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
                .Verifiable(Times.Never);
            return this;
        }

        internal TestBackgroundProcess Expect_LogMessages(params string[] expectedMessages)
        {
            _expectedLogMessages.AddRange(expectedMessages);
            return this;
        }

        internal TestBackgroundProcess Expect_ExecuteAsyncDoesNotComplete()
        {
            _mockMethods
                .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
                .Returns(new TaskCompletionSource().Task);
            return this;
        }
    }

    public interface IMockMethods
    {
        Task ExecuteAsync(CancellationToken stopToken);

        Task ExecuteOnThreadAsync(Func<CancellationToken, Task> task, CancellationToken cancellationToken);
    }
}
