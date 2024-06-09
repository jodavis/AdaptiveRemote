using AdaptiveRemote.Models;
using AdaptiveRemote.TestUtilities;
using Moq;

namespace AdaptiveRemote.Services.Commands;

[TestClass]
public class CommandServiceTests
{
    private static readonly Task IncompleteTask = new TaskCompletionSource().Task;

    private readonly Mock<IApplicationService> MockApplicationService = new();
    private readonly Mock<ITiVoService> MockTiVoService = new();
    private readonly Mock<IBroadlinkService> MockBroadlinkService = new();
    private readonly MockLogger<CommandService> MockLogger = new();

    private static readonly Command[] TestCommands =
    [
        new ApplicationCommand("Exit"),
        new TiVoCommand("Play"),
    ];

    [TestInitialize]
    public void SetupMocks()
    {
        MockApplicationService
            .Setup(x => x.Exit())
            .Verifiable(Times.Never);
        MockTiVoService
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        MockBroadlinkService
            .Setup(x => x.SendAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockApplicationService.Verify();
        MockTiVoService.Verify();
        MockBroadlinkService.Verify();
    }

    [TestMethod]
    [DataRow("Exit")]
    public void CommandService_Application_ExecuteAsync_ExecutesCommand(string command)
    {
        // Arrange
        ApplicationCommand input = new(command);

        Expect_ApplicationCommand(command);

        ICommandService sut = CreateSut();

        // Act
        Task resultTask = sut.ExecuteAsync(input);

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expect_ExecutingMessage(input),
            Expect_ExecutedMessage(input));
    }

    [TestMethod]
    public void CommandService_TiVo_ExecuteAsync_ExecutesCommand()
    {
        // Arrange
        TiVoCommand input = new("Play");

        Expect_TiVoCommand("PLAY");

        ICommandService sut = CreateSut();

        // Act
        Task resultTask = sut.ExecuteAsync(input);

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expect_ExecutingMessage(input),
            Expect_ExecutedMessage(input));
    }

    [TestMethod]
    public void CommandService_TiVo_ExecuteAsync_WaitsForCommandToComplete()
    {
        // Arrange
        TiVoCommand input = new("Play");

        Expect_TiVoCommand("PLAY", result: IncompleteTask);

        ICommandService sut = CreateSut();

        // Act
        Task resultTask = sut.ExecuteAsync(input);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expect_ExecutingMessage(input));
    }

    [TestMethod]
    public void CommandService_TiVo_ExecuteAsync_CancelsCommand()
    {
        // Arrange
        TiVoCommand input = new("Play");
        CancellationTokenSource cts = new();

        Expect_TiVoCommand("PLAY", IncompleteTask);

        ICommandService sut = CreateSut();

        Task resultTask = sut.ExecuteAsync(input, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expect_ExecutingMessage(input),
            Expect_CancelledMessage(input));
    }

    [TestMethod]
    public void CommandService_TiVo_ExecuteAsync_LogsError()
    {
        // Arrange
        TiVoCommand input = new("Play");

        Exception error = new InvalidOperationException("What the?!?");
        Expect_TiVoCommand("PLAY", Task.FromException(error));

        ICommandService sut = CreateSut();

        // Act
        Task resultTask = sut.ExecuteAsync(input);

        // Assert
        TaskAssert.IsFaulted(resultTask, error, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expect_ExecutingMessage(input),
            Expect_ErrorMessage(input, error));
    }

    [TestMethod]
    public void CommandService_Broadlink_ExecuteAsync_ExecutesCommand()
    {
        // Arrange
        BroadlinkCommand input = new("Mute");

        Expect_BroadlinkCommand();

        ICommandService sut = CreateSut();

        // Act
        Task resultTask = sut.ExecuteAsync(input);

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expect_ExecutingMessage(input),
            Expect_ExecutedMessage(input));
    }

    [TestMethod]
    public void CommandService_Broadlink_ExecuteAsync_WaitsForCommandToComplete()
    {
        // Arrange
        BroadlinkCommand input = new("Mute");

        Expect_BroadlinkCommand(result: IncompleteTask);

        ICommandService sut = CreateSut();

        // Act
        Task resultTask = sut.ExecuteAsync(input);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expect_ExecutingMessage(input));
    }

    [TestMethod]
    public void CommandService_Broadlink_ExecuteAsync_CancelsCommand()
    {
        // Arrange
        BroadlinkCommand input = new("Mute");
        CancellationTokenSource cts = new();

        Expect_BroadlinkCommand(IncompleteTask);

        ICommandService sut = CreateSut();

        Task resultTask = sut.ExecuteAsync(input, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expect_ExecutingMessage(input),
            Expect_CancelledMessage(input));
    }

    [TestMethod]
    public void CommandService_Broadlink_ExecuteAsync_LogsError()
    {
        // Arrange
        BroadlinkCommand input = new("Mute");

        Exception error = new InvalidOperationException("What the?!?");
        Expect_BroadlinkCommand(Task.FromException(error));

        ICommandService sut = CreateSut();

        // Act
        Task resultTask = sut.ExecuteAsync(input);

        // Assert
        TaskAssert.IsFaulted(resultTask, error, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expect_ExecutingMessage(input),
            Expect_ErrorMessage(input, error));
    }

    [TestMethod]
    public void CommandService_Unsupported_ExecuteAsync_LogsError()
    {
        // Arrange
        UnsupportedCommand input = new();

        ICommandService sut = CreateSut();

        Exception expectedError = new ArgumentException(Phrases.Commands_UnsupportedCommandType(input), "command");

        // Act
        Task resultTask = sut.ExecuteAsync(input);

        // Assert
        TaskAssert.IsFaulted(resultTask, expectedError, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expect_ExecutingMessage(input),
            Expect_ErrorMessage(input, expectedError));
    }

    private ICommandService CreateSut() => new CommandService(MockApplicationService.Object, MockTiVoService.Object, MockBroadlinkService.Object, MockLogger);

    private static string Expect_ExecutingMessage(Command command)
        => $"Information[601]: {string.Format(Logging.LoggingMessages.CommandService_Executing, command)}";
    private static string Expect_ExecutedMessage(Command command)
        => $"Information[602]: {string.Format(Logging.LoggingMessages.CommandService_Executed, command)}";
    private static string Expect_ErrorMessage(Command command, Exception error)
        => $"Error[603]: {string.Format(Logging.LoggingMessages.CommandService_Error, command, error)}";
    private static string Expect_CancelledMessage(Command command)
        => $"Warning[604]: {string.Format(Logging.LoggingMessages.CommandService_Cancelled, command)}";

    private void Expect_ApplicationCommand(string name, Times? times = default)
    {
        switch (name)
        {
            case nameof(IApplicationService.Exit):
                MockApplicationService
                    .Setup(x => x.Exit())
                    .Verifiable(times ?? Times.Once());
                break;
            default:
                Assert.Fail("Unknoan IApplicationService command '{0}'", name);
                break;
        }
    }

    private void Expect_TiVoCommand(string comandCode, Task? result = default, Times? times = default)
    {
        TaskCompletionSource tcs = WrapResult(result);

        MockTiVoService
            .Setup(x => x.SendAsync(comandCode, It.IsAny<CancellationToken>()))
            .Callback(delegate (string _, CancellationToken cancel) { cancel.Register(() => tcs.TrySetCanceled()); })
            .Returns(tcs.Task)
            .Verifiable(times ?? Times.Once());
    }

    private void Expect_BroadlinkCommand(Task? result = default, Times? times = default)
    {
        TaskCompletionSource tcs = WrapResult(result);

        MockBroadlinkService
            .Setup(x => x.SendAsync(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { cancel.Register(() => tcs.TrySetCanceled()); })
            .Returns(tcs.Task)
            .Verifiable(times ?? Times.Once());
    }

    private static TaskCompletionSource WrapResult(Task? result)
    {
        TaskCompletionSource tcs = new();
        if (result is not null)
        {
            result.ContinueWith(_ => result.IsFaulted ? tcs.TrySetException(result.Exception.InnerException!) : tcs.TrySetResult(), TaskContinuationOptions.ExecuteSynchronously);
        }
        else
        {
            tcs.SetResult();
        }

        return tcs;
    }

    private class UnsupportedCommand : Command
    {
        public UnsupportedCommand()
            : base(nameof(UnsupportedCommand), null, null, null, null)
        { }
    }
}
