using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using AdaptiveRemote.TestUtilities;
using Microsoft.Extensions.Options;
using Moq;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class ConversationControllerTests
{
    private static readonly Task IncompleteTask = new TaskCompletionSource().Task;

    private readonly MockLogger<ConversationController> MockLogger = new();
    private readonly Mock<ISpeechRecognition> MockRecognition = new();
    private readonly Mock<ISpeechSynthesis> MockSynthesis = new();
    private readonly Mock<ICommandExecutionService> MockExecution = new();
    private readonly Mock<IRemoteDefinitionService> MockDefinition = new();
    private readonly Mock<IOptionsSnapshot<ConversationSettings>> MockOptions = new();
    private readonly ConversationSettings ConversationSettings = new();

    private static readonly Models.TiVoCommand Command1 = new("Hey you!");
    private static readonly Models.TiVoCommand Command2 = new("Test Two");

    private readonly Models.ConversationView ViewModel = new("MOCKGROUP");
    private readonly Models.RemoteLayoutElement RootLayout =
        new Models.LayoutGroup("COMMANDS", new List<Models.RemoteLayoutElement>
        {
            Command1,
            Command2
        });

    private static string Expected_ListenForAttention
        => $"Information[209]: {LoggingMessages.ConversationController_ListenForAttention}";
    private static string Expected_ListenForCommands
        => $"Information[212]: {LoggingMessages.ConversationController_ListenForCommands}";
    private static string Expected_Recognized(string text, string semantics)
        => $"Information[207]: {string.Format(LoggingMessages.ConversationController_Recognized, text, semantics)}";
    private static string Expected_Executing(string command)
        => $"Information[210]: {string.Format(LoggingMessages.ConversationController_Executing, command)}";
    private static string Expected_Executed(string command)
        => $"Information[211]: {string.Format(LoggingMessages.ConversationController_Executed, command)}";
    private static string Expected_UnknownCommand(string command)
        => $"Error[208]: {string.Format(LoggingMessages.ConversationController_UnknownCommand, command)}";
    private static string Expected_ErrorDuringStartup(Exception error)
        => $"Error[201]: {string.Format(LoggingMessages.ConversationController_ErrorDuringStartup, error)}";
    private static string Expected_Error(Exception error)
        => $"Error[204]: {string.Format(LoggingMessages.ConversationController_Error, error)}";
    private static string Expected_Retrying(int times)
        => $"Warning[206]: {string.Format(LoggingMessages.ConversationController_Retrying, times)}";
    private static string Expected_RetryLimitReached(int times)
        => $"Warning[205]: {string.Format(LoggingMessages.ConversationController_RetryLimitReached, times)}";
    private static string Expected_Stopping
        => $"Information[202]: {LoggingMessages.ConversationController_Stopping}";
    private static string Expected_Stopped
        => $"Information[203]: {LoggingMessages.ConversationController_Stopped}";

    private ConversationController CreateSut() => new(
        MockOptions.Object,
        MockRecognition.Object,
        MockSynthesis.Object,
        MockDefinition.Object,
        MockExecution.Object,
        MockLogger,
        ViewModel);

    private static Mock<IRecognitionResult> CreateMockResult(string text, params string[] semanticValues)
    {
        string? nullValue;

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .Setup(x => x.ContainsSemanticValue(It.IsAny<string>()))
            .Returns(false);
        mockResult
            .Setup(x => x.TryGetSemanticValue(It.IsAny<string>(), out nullValue))
            .Returns(false);

        mockResult
            .SetupGet(x => x.Text)
            .Returns(text);

        foreach (string semanticValue in semanticValues)
        {
            string[] parts = semanticValue.Split('=');
            string key = parts[0];
            string? value = parts[1];

            mockResult
                .Setup(x => x.ContainsSemanticValue(key))
                .Returns(true);
            mockResult
                .Setup(x => x.TryGetSemanticValue(key, out value))
                .Returns(true);
        }

        return mockResult;
    }

    [TestInitialize]
    public void InitializeMocks()
    {
        MockDefinition
            .SetupGet(x => x.RemoteRoot)
            .Returns(RootLayout)
            .Verifiable(Times.Once);

        MockExecution
            .Setup(x => x.ExecuteAsync(It.IsAny<Models.Command>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);

        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);

        MockSynthesis
            .Setup(x => x.Say(It.IsAny<string>()))
            .Verifiable(Times.Never);

        MockOptions
            .SetupGet(x => x.Value)
            .Returns(ConversationSettings)
            .Verifiable(Times.Once);
    }

    [TestCleanup]
    public void VerifyAllMocks()
    {
        MockRecognition.Verify();
        MockSynthesis.Verify();
        MockExecution.Verify();
        MockDefinition.Verify();
    }

    [TestMethod]
    public void ConversationController_OnConstruction_InitializesViewModel()
    {
        // Arrange
        ViewModel.IsListening = true;
        ViewModel.StatusMessage = "Status message was not changed";

        MockDefinition
            .SetupGet(x => x.RemoteRoot)
            .Verifiable(Times.Never);

        // Act
        IConversationController sut = CreateSut();

        // Assert
        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_WaitingForActivation, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnErrorDuringInitialization_LogsError()
    {
        // Arrange
        IConversationController sut = CreateSut();

        Exception exception = new DataMisalignedException();
        MockDefinition
            .SetupGet(x => x.RemoteRoot)
            .Throws(exception);

        // Act
        sut.StartListening();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ErrorDuringStartup(exception));

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_SystemFailed, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_Start_StartsListeningForAttention()
    {
        // Arrange
        IConversationController sut = CreateSut();

        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        // Act
        sut.StartListening();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnAttention_StartsListeningForCommands()
    {
        // Arrange
        IConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(false))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_ImListening))
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_ListenForCommands);

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnFirstCommand_AnnouncesAndExecutesCommand()
    {
        // Arrange
        IConversationController sut = CreateSut();

        Mock<IRecognitionResult> result = CreateMockResult(Command1.Name, "command=" + Command1.Name);

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(false, result.Object))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_ImListening))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_Sent(Command1.Name)))
            .Verifiable(Times.Once);

        MockExecution
            .Setup(x => x.ExecuteAsync(Command1, It.IsAny<CancellationToken>()))
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Recognized(result.Object.Text, Command1.Name),
            Expected_Executing(Command1.Name));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ImSending, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnUnrecognizedCommand_LogsError()
    {
        // Arrange
        IConversationController sut = CreateSut();

        Mock<IRecognitionResult> result = CreateMockResult("Not a command", "command=Not a command");

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(false, result.Object))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_ImListening))
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Recognized(result.Object.Text, result.Object.Text),
            Expected_UnknownCommand("Not a command"));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnCompletedFirstCommand_LogsExecutedCommand()
    {
        // Arrange
        IConversationController sut = CreateSut();

        Mock<IRecognitionResult> result1 = CreateMockResult(Command1.Name, "command=" + Command1.Name);

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(false, result1.Object))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_ImListening))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_Sent(Command1.Name)))
            .Verifiable(Times.Once);

        MockExecution
            .Setup(x => x.ExecuteAsync(Command1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Recognized(result1.Object.Text, Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnCommandWithRepeat_LogsExecutedCommandMultipleTimes()
    {
        // Arrange
        IConversationController sut = CreateSut();

        Mock<IRecognitionResult> result1 = CreateMockResult(Command1.Name, "command=" + Command1.Name, "repeat=3");

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(false, result1.Object))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_ImListening))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_Sent(Command1.Name, 3)))
            .Verifiable(Times.Once);

        MockExecution
            .Setup(x => x.ExecuteAsync(Command1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(3));

        sut.StartListening();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Recognized(result1.Object.Text, Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnAttentionAndStopListening_StartsListeningForAttentionAgain()
    {
        // Arrange
        IConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => tcs = new())
            .Returns(() => tcs.Task)
            .Verifiable(Times.Exactly(2));
        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(true))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_ImListening))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_StoppedListening))
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnAttentionAndStopListening_StopsAndStartsListeningForCommandsAgain()
    {
        // Arrange
        IConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => tcs = new())
            .Returns(() => tcs.Task)
            .Verifiable(Times.Exactly(3));
        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(true))
            .Verifiable(Times.Exactly(2));

        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_ImListening))
            .Verifiable(Times.Exactly(2));
        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_StoppedListening))
            .Verifiable(Times.Exactly(2));

        sut.StartListening();
        tcs.SetResult();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnError_RestartsListeningForAttention()
    {
        // Arrange
        IConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        AccessViolationException exception = new AccessViolationException("Whoopsie!");
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => tcs = new())
            .Returns(() => tcs.Task)
            .Verifiable(Times.Exactly(2));
        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Throws(exception)
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_ImListening))
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Error(exception),
            Expected_Retrying(1),
            Expected_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    [Timeout(1000)]
    public void ConversationController_OnRepeatedErrors_StopsRestartingAfterErrorLimit()
    {
        // Arrange
        IConversationController sut = CreateSut();

        AccessViolationException exception = new AccessViolationException("Whoopsie!");
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Throws(exception)
            .Verifiable(Times.Exactly(ConversationSettings.ErrorRetryLimit));

        // Act
        sut.StartListening();

        // Assert
        string expectedErrorMessage = Expected_Error(exception);
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            expectedErrorMessage,
            Expected_Retrying(1),
            Expected_ListenForAttention,
            expectedErrorMessage,
            Expected_Retrying(2),
            Expected_ListenForAttention,
            expectedErrorMessage,
            Expected_Retrying(3),
            Expected_ListenForAttention,
            expectedErrorMessage,
            Expected_Retrying(4),
            Expected_ListenForAttention,
            expectedErrorMessage,
            Expected_Retrying(5),
            Expected_ListenForAttention,
            expectedErrorMessage,
            Expected_Retrying(6),
            Expected_ListenForAttention,
            expectedErrorMessage,
            Expected_Retrying(7),
            Expected_ListenForAttention,
            expectedErrorMessage,
            Expected_Retrying(8),
            Expected_ListenForAttention,
            expectedErrorMessage,
            Expected_Retrying(9),
            Expected_ListenForAttention,
            expectedErrorMessage,
            Expected_RetryLimitReached(10));

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_SystemFailed, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_StopWhileWaitingForAttention_CancelsWaitingForAttention()
    {
        // Arrange
        IConversationController sut = CreateSut();

        CancellationToken token = default;
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { token = cancel; })
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        sut.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_Stopping);

        Assert.IsTrue(token.IsCancellationRequested, nameof(token.IsCancellationRequested));

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_StopCancelsListenForAttention_LogsStopped()
    {
        // Arrange
        IConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { cancel.Register(tcs.SetCanceled); })
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        sut.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_Stopping,
            Expected_Stopped);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(string.Empty, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_StopWhileWaitingForCommands_CancelsWaitingForCommands()
    {
        // Arrange
        IConversationController sut = CreateSut();

        CancellationToken token = default;
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_ImListening))
            .Verifiable(Times.Once);

        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { token = cancel; })
            .Returns(AsyncEnumerate(complete: false))
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        sut.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Stopping);

        Assert.IsTrue(token.IsCancellationRequested, nameof(token.IsCancellationRequested));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_WaitingForCommandsCanceled_LogsStopped()
    {
        // Arrange
        IConversationController sut = CreateSut();

        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_ImListening))
            .Verifiable(Times.Once);

        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(CancelAsyncEnumerate())
            .Verifiable(Times.Once);

        // Act
        sut.StartListening();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Stopped);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(string.Empty, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_StopWhileExecutingCommand_CancelsExecutingCommand()
    {
        // Arrange
        IConversationController sut = CreateSut();

        CancellationToken token = default;
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_ImListening))
            .Verifiable(Times.Once);

        Mock<IRecognitionResult> result1 = CreateMockResult(Command1.Name, "command=" + Command1.Name);

        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(complete: false, result1.Object))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_Sent(Command1.Name)))
            .Verifiable(Times.Once);

        MockExecution
            .Setup(x => x.ExecuteAsync(Command1, It.IsAny<CancellationToken>()))
            .Callback(delegate (Models.Command command, CancellationToken cancel) { token = cancel; })
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        sut.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Recognized(result1.Object.Text, Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Stopping);

        Assert.IsTrue(token.IsCancellationRequested, nameof(token.IsCancellationRequested));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ImSending, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_ExecutingCommandCanceled_LogsStopped()
    {
        // Arrange
        IConversationController sut = CreateSut();

        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_ImListening))
            .Verifiable(Times.Once);

        Mock<IRecognitionResult> result1 = CreateMockResult(Command1.Name, "command=" + Command1.Name);

        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(complete: false, result1.Object))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Conversation_Sent(Command1.Name)))
            .Verifiable(Times.Once);

        TaskCompletionSource tcs = new();
        MockExecution
            .Setup(x => x.ExecuteAsync(Command1, It.IsAny<CancellationToken>()))
            .Callback(delegate (Models.Command command, CancellationToken cancel) { cancel.Register(tcs.SetCanceled); })
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        sut.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Recognized(result1.Object.Text, Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Stopping,
            Expected_Stopped);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(string.Empty, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    private static async IAsyncEnumerable<IRecognitionResult> AsyncEnumerate(bool complete, params IRecognitionResult[] commands)
    {
        foreach (IRecognitionResult command in commands)
        {
            yield return command;
        }

        if (!complete)
        {
            await IncompleteTask;
        }
    }

    private static async IAsyncEnumerable<IRecognitionResult> CancelAsyncEnumerate(bool cancel = true)
    {
        if (cancel)
        {
            throw new TaskCanceledException();
        }

        await Task.Yield();
        yield break;
    }
}
