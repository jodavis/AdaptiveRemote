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

    private readonly Models.Conversation ViewModel = new("MOCKGROUP");
    private readonly Models.RemoteLayoutElement RootLayout =
        new Models.LayoutGroup("COMMANDS", new List<Models.RemoteLayoutElement>
        {
            Command1,
            Command2
        });

    private ConversationController CreateSut() => new(
        MockOptions.Object,
        MockRecognition.Object,
        MockSynthesis.Object,
        MockDefinition.Object,
        MockExecution.Object,
        MockLogger,
        ViewModel);

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
            .Setup(x => x.ListenForCommands(It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
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
        Assert.AreEqual(Phrases.Speech_WaitingForActivation, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
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
            string.Format(LoggingMessages.ConversationController_ErrorDuringStartup, exception));

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ListeningSystemFailed, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_Start_StartsListeningForAttention()
    {
        // Arrange
        IConversationController sut = CreateSut();

        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        // Act
        sut.StartListening();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.ConversationController_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnAttention_StartsListeningForCommands()
    {
        // Arrange
        IConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommands(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(false))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_ImListening))
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_ListenForCommands);

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnFirstCommand_AnnouncesAndExecutesCommand()
    {
        // Arrange
        IConversationController sut = CreateSut();

        Mock<IRecognitionResult> result = new();
        result
            .SetupGet(x => x.Text)
            .Returns(Command1.Name);
        result
            .SetupGet(x => x.SemanticMeaning)
            .Returns("Command:" + Command1.Name);

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommands(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(false, result.Object))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_ImListening))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_Sending(Command1.Name)))
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
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_ListenForCommands,
            string.Format(LoggingMessages.ConversationController_Recognized, result.Object.Text, result.Object.SemanticMeaning),
            string.Format(LoggingMessages.ConversationController_Executing, Command1.Name));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ImSending, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnUnrecognizedCommand_LogsError()
    {
        // Arrange
        IConversationController sut = CreateSut();

        Mock<IRecognitionResult> result = new();
        result
            .SetupGet(x => x.Text)
            .Returns("Not a command");
        result
            .SetupGet(x => x.SemanticMeaning)
            .Returns("Command:Not a command");

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommands(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(false, result.Object))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_ImListening))
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_ListenForCommands,
            string.Format(LoggingMessages.ConversationController_Recognized, result.Object.Text, result.Object.SemanticMeaning),
            string.Format(LoggingMessages.ConversationController_UnknownCommand, "Not a command"));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnCompletedFirstCommand_LogsExecutedCommand()
    {
        // Arrange
        IConversationController sut = CreateSut();

        Mock<IRecognitionResult> result1 = new();
        result1
            .SetupGet(x => x.SemanticMeaning)
            .Returns("Command:" + Command1.Name);
        result1
            .SetupGet(x => x.Text)
            .Returns(Command1.Name);

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommands(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(false, result1.Object))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_ImListening))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_Sending(Command1.Name)))
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
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_ListenForCommands,
            string.Format(LoggingMessages.ConversationController_Recognized, result1.Object.Text, result1.Object.SemanticMeaning),
            string.Format(LoggingMessages.ConversationController_Executing, Command1.Name),
            string.Format(LoggingMessages.ConversationController_Executed, Command1.Name));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnAttentionAndStopListening_StartsListeningForAttentionAgain()
    {
        // Arrange
        IConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Callback(() => tcs = new())
            .Returns(() => tcs.Task)
            .Verifiable(Times.Exactly(2));
        MockRecognition
            .Setup(x => x.ListenForCommands(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(true))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_ImListening))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_StoppedListening))
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_ListenForCommands,
            LoggingMessages.ConversationController_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnAttentionAndStopListening_StopsAndStartsListeningForCommandsAgain()
    {
        // Arrange
        IConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Callback(() => tcs = new())
            .Returns(() => tcs.Task)
            .Verifiable(Times.Exactly(3));
        MockRecognition
            .Setup(x => x.ListenForCommands(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(true))
            .Verifiable(Times.Exactly(2));

        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_ImListening))
            .Verifiable(Times.Exactly(2));
        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_StoppedListening))
            .Verifiable(Times.Exactly(2));

        sut.StartListening();
        tcs.SetResult();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_ListenForCommands,
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_ListenForCommands,
            LoggingMessages.ConversationController_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_OnError_RestartsListeningForAttention()
    {
        // Arrange
        IConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        AccessViolationException exception = new AccessViolationException("Whoopsie!");
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Callback(() => tcs = new())
            .Returns(() => tcs.Task)
            .Verifiable(Times.Exactly(2));
        MockRecognition
            .Setup(x => x.ListenForCommands(It.IsAny<CancellationToken>()))
            .Throws(exception)
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_ImListening))
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_ListenForCommands,
            string.Format(LoggingMessages.ConversationController_Error, exception),
            string.Format(LoggingMessages.ConversationController_Retrying, 1),
            LoggingMessages.ConversationController_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    [Timeout(1000)]
    public void ConversationController_OnRepeatedErrors_StopsRestartingAfterErrorLimit()
    {
        // Arrange
        IConversationController sut = CreateSut();

        AccessViolationException exception = new AccessViolationException("Whoopsie!");
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Throws(exception)
            .Verifiable(Times.Exactly(ConversationSettings.ErrorRetryLimit));

        // Act
        sut.StartListening();

        // Assert
        string expectedErrorMessage = string.Format(LoggingMessages.ConversationController_Error, exception);
        MockLogger.VerifyMessages(
            LoggingMessages.ConversationController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.ConversationController_Retrying, 1),
            LoggingMessages.ConversationController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.ConversationController_Retrying, 2),
            LoggingMessages.ConversationController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.ConversationController_Retrying, 3),
            LoggingMessages.ConversationController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.ConversationController_Retrying, 4),
            LoggingMessages.ConversationController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.ConversationController_Retrying, 5),
            LoggingMessages.ConversationController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.ConversationController_Retrying, 6),
            LoggingMessages.ConversationController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.ConversationController_Retrying, 7),
            LoggingMessages.ConversationController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.ConversationController_Retrying, 8),
            LoggingMessages.ConversationController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.ConversationController_Retrying, 9),
            LoggingMessages.ConversationController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.ConversationController_RetryLimitReached, 10));

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ListeningSystemFailed, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_StopWhileWaitingForAttention_CancelsWaitingForAttention()
    {
        // Arrange
        IConversationController sut = CreateSut();

        CancellationToken token = default;
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { token = cancel; })
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        sut.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_Stopping);

        Assert.IsTrue(token.IsCancellationRequested, nameof(token.IsCancellationRequested));

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_StopCancelsListenForAttention_LogsStopped()
    {
        // Arrange
        IConversationController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { cancel.Register(tcs.SetCanceled); })
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        sut.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_Stopping,
            LoggingMessages.ConversationController_Stopped);

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
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_ImListening))
            .Verifiable(Times.Once);

        MockRecognition
            .Setup(x => x.ListenForCommands(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { token = cancel; })
            .Returns(AsyncEnumerate(complete: false))
            .Verifiable(Times.Once);

        sut.StartListening();

        // Act
        sut.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_ListenForCommands,
            LoggingMessages.ConversationController_Stopping);

        Assert.IsTrue(token.IsCancellationRequested, nameof(token.IsCancellationRequested));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_WaitingForCommandsCanceled_LogsStopped()
    {
        // Arrange
        IConversationController sut = CreateSut();

        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_ImListening))
            .Verifiable(Times.Once);

        MockRecognition
            .Setup(x => x.ListenForCommands(It.IsAny<CancellationToken>()))
            .Returns(CancelAsyncEnumerate())
            .Verifiable(Times.Once);

        // Act
        sut.StartListening();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_ListenForCommands,
            LoggingMessages.ConversationController_Stopped);

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
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_ImListening))
            .Verifiable(Times.Once);

        Mock<IRecognitionResult> result1 = new();
        result1
            .SetupGet(x => x.Text)
            .Returns(Command1.Name);
        result1
            .SetupGet(x => x.SemanticMeaning)
            .Returns("Command:" + Command1.Name);

        MockRecognition
            .Setup(x => x.ListenForCommands(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(complete: false, result1.Object))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_Sending(Command1.Name)))
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
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_ListenForCommands,
            string.Format(LoggingMessages.ConversationController_Recognized, result1.Object.Text, result1.Object.SemanticMeaning),
            string.Format(LoggingMessages.ConversationController_Executing, Command1.Name),
            LoggingMessages.ConversationController_Stopping);

        Assert.IsTrue(token.IsCancellationRequested, nameof(token.IsCancellationRequested));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ImSending, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void ConversationController_ExecutingCommandCanceled_LogsStopped()
    {
        // Arrange
        IConversationController sut = CreateSut();

        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_ImListening))
            .Verifiable(Times.Once);

        Mock<IRecognitionResult> result1 = new();
        result1
            .SetupGet(x => x.Text)
            .Returns(Command1.Name);
        result1
            .SetupGet(x => x.SemanticMeaning)
            .Returns("Command:" + Command1.Name);

        MockRecognition
            .Setup(x => x.ListenForCommands(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(complete: false, result1.Object))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_Sending(Command1.Name)))
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
            LoggingMessages.ConversationController_ListenForAttention,
            LoggingMessages.ConversationController_ListenForCommands,
            string.Format(LoggingMessages.ConversationController_Recognized, result1.Object.Text, result1.Object.SemanticMeaning),
            string.Format(LoggingMessages.ConversationController_Executing, Command1.Name, Command1.Name),
            LoggingMessages.ConversationController_Stopping,
            LoggingMessages.ConversationController_Stopped);

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
