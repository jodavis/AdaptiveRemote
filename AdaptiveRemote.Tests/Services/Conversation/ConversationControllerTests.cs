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
    private readonly Mock<IRemoteDefinitionService> MockDefinition = new();
    private readonly Mock<IOptionsSnapshot<ConversationSettings>> MockOptions = new();
    private readonly ConversationSettings ConversationSettings = new();
    private readonly Mock<CommandExecute> Command1Execute = new();

    private readonly TiVoCommand Command1 = new("Hey you!");
    private readonly TiVoCommand Command2 = new("Test Two");

    private readonly ConversationView ViewModel = new("MOCKGROUP");

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
    private static string Expected_Retrying(int times, Exception latestError)
        => $"Warning[206]: {string.Format(LoggingMessages.ConversationController_Retrying, times, latestError)}";
    private static string Expected_RetryLimitReached(int times)
        => $"Warning[205]: {string.Format(LoggingMessages.ConversationController_RetryLimitReached, times)}";
    private static string Expected_Starting
        => $"Information[1201]: {LoggingMessages.ScopedBackgroundProcess_Starting}";
    private static string Expected_Started
        => $"Information[1202]: {LoggingMessages.ScopedBackgroundProcess_Started}";
    private static string Expected_Stopping
        => $"Information[1203]: {LoggingMessages.ScopedBackgroundProcess_Stopping}";
    private static string Expected_Stopped
        => $"Information[1204]: {LoggingMessages.ScopedBackgroundProcess_Stopped}";
    private static string Expected_StoppedEarly
        => $"Warning[1205]: {LoggingMessages.ScopedBackgroundProcess_StoppedEarly}";
    private static string Expected_Error(Exception error)
        => $"Error[1206]: {string.Format(LoggingMessages.ScopedBackgroundProcess_Error, error)}";
    private static string Expected_CommandMissingExecuteAction(string command)
        => $"Error[213]: {string.Format(LoggingMessages.ConversationController_CommandMissingExecuteAction, command)}";
    private static string Expected_CommandDisabled(string command)
        => $"Error[214]: {string.Format(LoggingMessages.ConversationController_CommandDisabled, command)}";

    private ConversationController CreateSut() => new(
        MockOptions.Object,
        MockRecognition.Object,
        MockSynthesis.Object,
        MockDefinition.Object,
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
            .Returns(new LayoutGroup("COMMANDS",
            [
                Command1,
                Command2
            ]))
            .Verifiable(Times.Once);

        Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        Command1.ExecuteAsync = Command1Execute.Object.ExecuteAsync;
        Command1.IsEnabled = true;

        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);

        MockSynthesis
            .Setup(x => x.SayAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
        Command1Execute.Verify();
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
        ConversationController sut = CreateSut();

        // Assert
        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_WaitingForActivation, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnErrorDuringInitialization_LogsError()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Exception exception = new DataMisalignedException();
        MockDefinition
            .SetupGet(x => x.RemoteRoot)
            .Throws(exception);

        // Act
        sut.InitializeAsync(default);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_Error(exception));

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_SystemFailed, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_Start_StartsListeningForAttention()
    {
        // Arrange
        ConversationController sut = CreateSut();

        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        // Act
        sut.InitializeAsync(default);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnAttention_WaitsToSayImListeningingBeforeListeningForCommands()
    {
        // Arrange
        ConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        sut.InitializeAsync(default);

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.AreEqual(Phrases.Conversation_ImListening, ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnAttention_CleanUpWaitsForSayingImListening()
    {
        // Arrange
        ConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started,
            Expected_Stopping);

        // Act
        tcs.SetResult();

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started,
            Expected_Stopping,
            Expected_Stopped);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(string.Empty, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnAttention_StartsListeningForCommands()
    {
        // Arrange
        ConversationController sut = CreateSut();

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
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        sut.InitializeAsync(default);

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Started);

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnFirstCommand_AnnouncesAndExecutesCommand()
    {
        // Arrange
        ConversationController sut = CreateSut();

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
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_Sent(Command1.Name), It.IsAny<CancellationToken>()))
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        sut.InitializeAsync(default);

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Recognized(result.Object.Text, Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Started);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ImSending, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.AreEqual(Phrases.Conversation_Sent(Command1.Name), ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnUnrecognizedCommand_LogsError()
    {
        // Arrange
        ConversationController sut = CreateSut();

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
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started,
            Expected_ListenForCommands,
            Expected_Recognized(result.Object.Text, result.Object.Text),
            Expected_UnknownCommand("Not a command"));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnCompletedFirstCommand_LogsExecutedCommand()
    {
        // Arrange
        ConversationController sut = CreateSut();

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
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_Sent(Command1.Name), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started,
            Expected_ListenForCommands,
            Expected_Recognized(result1.Object.Text, Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnCompletedFirstCommand_DoesNotExecuteNextCommandUntilSayAsyncCompletes()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Mock<IRecognitionResult> result1 = CreateMockResult(Command1.Name, "command=" + Command1.Name);
        Mock<IRecognitionResult> result2 = CreateMockResult(Command2.Name, "command=" + Command2.Name);

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(false, result1.Object, result2.Object))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_Sent(Command1.Name), It.IsAny<CancellationToken>()))
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started,
            Expected_ListenForCommands,
            Expected_Recognized(result1.Object.Text, Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name));

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ImSending, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.AreEqual(Phrases.Conversation_Sent(Command1.Name), ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnCompletedFirstCommand_CleanUpWaitsForSayingSent()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Mock<IRecognitionResult> result1 = CreateMockResult(Command1.Name, "command=" + Command1.Name);

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(false, result1.Object))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_Sent(Command1.Name), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Recognized(result1.Object.Text, Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name),
            Expected_Started,
            Expected_Stopping);

        // Act
        tcs.SetResult();

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Recognized(result1.Object.Text, Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Executed(Command1.Name),
            Expected_Started,
            Expected_Stopping,
            Expected_Stopped);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(string.Empty, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnCommandWithRepeat_LogsExecutedCommandMultipleTimes()
    {
        // Arrange
        ConversationController sut = CreateSut();

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
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_Sent(Command1.Name, 3), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(3));

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started,
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
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnAttentionAndStopListening_WaitsToSayStoppedListeningBeforeListeningForAttention()
    {
        // Arrange
        ConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => tcs = new())
            .Returns(() => tcs.Task)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(true))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_StoppedListening, It.IsAny<CancellationToken>()))
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started,
            Expected_ListenForCommands);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(string.Empty, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.AreEqual(Phrases.Conversation_StoppedListening, ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnAttentionAndStopListening_CleanUpWaitsForSayAsync()
    {
        // Arrange
        ConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(true))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_StoppedListening, It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Started,
            Expected_Stopping);

        // Act
        tcs.SetResult();

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Started,
            Expected_Stopping,
            Expected_Stopped);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ShuttingDown, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnAttentionAndStopListening_StartsListeningForAttentionAgain()
    {
        // Arrange
        ConversationController sut = CreateSut();

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
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_StoppedListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started,
            Expected_ListenForCommands,
            Expected_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnAttentionAndStopListening_StopsAndStartsListeningForCommandsAgain()
    {
        // Arrange
        ConversationController sut = CreateSut();

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
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Exactly(2));
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_StoppedListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Exactly(2));

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");
        tcs.SetResult();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started,
            Expected_ListenForCommands,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnError_RestartsListeningForAttention()
    {
        // Arrange
        ConversationController sut = CreateSut();

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
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started,
            Expected_ListenForCommands,
            Expected_Retrying(1, exception),
            Expected_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    [Timeout(1000)]
    public void ConversationController_OnRepeatedErrors_StopsRestartingAfterErrorLimit()
    {
        // Arrange
        ConversationController sut = CreateSut();

        AccessViolationException exception = new AccessViolationException("Whoopsie!");
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Throws(exception)
            .Verifiable(Times.Exactly(ConversationSettings.ErrorRetryLimit));

        // Act
        sut.InitializeAsync(default);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Retrying(1, exception),
            Expected_ListenForAttention,
            Expected_Retrying(2, exception),
            Expected_ListenForAttention,
            Expected_Retrying(3, exception),
            Expected_ListenForAttention,
            Expected_Retrying(4, exception),
            Expected_ListenForAttention,
            Expected_Retrying(5, exception),
            Expected_ListenForAttention,
            Expected_Retrying(6, exception),
            Expected_ListenForAttention,
            Expected_Retrying(7, exception),
            Expected_ListenForAttention,
            Expected_Retrying(8, exception),
            Expected_ListenForAttention,
            Expected_Retrying(9, exception),
            Expected_ListenForAttention,
            Expected_RetryLimitReached(10),
            Expected_Error(exception));

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_SystemFailed, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_CleanUpWhileWaitingForAttention_CancelsWaitingForAttention()
    {
        // Arrange
        ConversationController sut = CreateSut();

        CancellationToken token = default;
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { token = cancel; })
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started,
            Expected_Stopping);

        Assert.IsTrue(token.IsCancellationRequested, nameof(token.IsCancellationRequested));

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ShuttingDown, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_CleanUpCancelsListenForAttention_LogsStopped()
    {
        // Arrange
        ConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { cancel.Register(tcs.SetCanceled); })
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_Started,
            Expected_Stopping,
            Expected_Stopped);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(string.Empty, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_CleanUpWhileWaitingForCommands_CancelsWaitingForCommands()
    {
        // Arrange
        ConversationController sut = CreateSut();

        CancellationToken token = default;
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { token = cancel; })
            .Returns(AsyncEnumerate(complete: false))
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Started,
            Expected_Stopping);

        Assert.IsTrue(token.IsCancellationRequested, nameof(token.IsCancellationRequested));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ShuttingDown, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_WaitingForCommandsCanceled_LogsStopped()
    {
        // Arrange
        ConversationController sut = CreateSut();

        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(CancelAsyncEnumerate())
            .Verifiable(Times.Once);

        // Act
        sut.InitializeAsync(default);

        // Assert
        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_StoppedEarly);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(string.Empty, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_CleanUpWhileExecutingCommand_CancelsExecutingCommand()
    {
        // Arrange
        ConversationController sut = CreateSut();

        CancellationToken token = default;
        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        Mock<IRecognitionResult> result1 = CreateMockResult(Command1.Name, "command=" + Command1.Name);

        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(complete: false, result1.Object))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_Sent(Command1.Name), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { token = cancel; })
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Recognized(result1.Object.Text, Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Started,
            Expected_Stopping);

        Assert.IsTrue(token.IsCancellationRequested, nameof(token.IsCancellationRequested));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Conversation_ShuttingDown, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_ExecutingCommandCanceled_LogsStopped()
    {
        // Arrange
        ConversationController sut = CreateSut();

        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        Mock<IRecognitionResult> result1 = CreateMockResult(Command1.Name, "command=" + Command1.Name);

        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(complete: false, result1.Object))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_Sent(Command1.Name), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        TaskCompletionSource tcs = new();
        Command1Execute
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { cancel.Register(tcs.SetCanceled); })
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        TaskAssert.IsComplete(sut.InitializeAsync(default), TimeSpan.FromSeconds(1), "InitializeAsync");

        // Act
        Task resultTask = sut.CleanUpAsync(default);

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Recognized(result1.Object.Text, Command1.Name),
            Expected_Executing(Command1.Name),
            Expected_Started,
            Expected_Stopping,
            Expected_Stopped);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(string.Empty, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
        Assert.IsNull(ViewModel.SpeakingMessage, nameof(ViewModel.SpeakingMessage));
    }

    [TestMethod]
    public void ConversationController_OnCommandWithoutExecuteAsync_SpeaksAndLogsError()
    {
        // Arrange
        ConversationController sut = CreateSut();

        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        Mock<IRecognitionResult> result1 = CreateMockResult(Command2.Name, "command=" + Command2.Name);

        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(complete: false, result1.Object))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_CommandDisabled(Command2.Name), It.IsAny<CancellationToken>()))
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = sut.InitializeAsync(default);

        // Assert
        TaskAssert.IsComplete(resultTask, TimeSpan.FromSeconds(1), nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Recognized(result1.Object.Text, Command2.Name),
            Expected_CommandMissingExecuteAction(Command2.Name),
            Expected_Started);
    }

    [TestMethod]
    public void ConversationController_OnCommandWithIsEnabledFalse_SpeaksAndLogsError()
    {
        // Arrange
        ConversationController sut = CreateSut();

        Command1.IsEnabled = false;

        MockRecognition
            .Setup(x => x.ListenForAttentionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_ImListening, It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        Mock<IRecognitionResult> result1 = CreateMockResult(Command1.Name, "command=" + Command1.Name);

        MockRecognition
            .Setup(x => x.ListenForCommandsAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(complete: false, result1.Object))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.SayAsync(Phrases.Conversation_CommandDisabled(Command1.Name), It.IsAny<CancellationToken>()))
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = sut.InitializeAsync(default);

        // Assert
        TaskAssert.IsComplete(resultTask, TimeSpan.FromSeconds(1), nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_Starting,
            Expected_ListenForAttention,
            Expected_ListenForCommands,
            Expected_Recognized(result1.Object.Text, Command1.Name),
            Expected_CommandDisabled(Command1.Name),
            Expected_Started);
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

    public abstract class CommandExecute
    {
        public abstract Task ExecuteAsync(CancellationToken cancellationToken);
    }
}
