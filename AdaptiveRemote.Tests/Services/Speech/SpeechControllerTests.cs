using System.Speech.Recognition;
using Microsoft.Extensions.Options;
using Moq;

namespace AdaptiveRemote.Services.Speech;

[TestClass]
public class SpeechControllerTests
{
    private static readonly Task IncompleteTask = new TaskCompletionSource().Task;

    private readonly MockLogger<SpeechController> MockLogger = new();
    private readonly Mock<ISpeechRecognition> MockRecognition = new();
    private readonly Mock<ISpeechSynthesis> MockSynthesis = new();
    private readonly Mock<ICommandExecutionService> MockExecution = new();
    private readonly Mock<IRemoteDefinitionService> MockDefinition = new();
    private readonly Mock<IOptionsSnapshot<SpeechSettings>> MockOptions = new();
    private readonly SpeechSettings SpeechSettings = new();

    private static readonly Models.TiVoCommand Command1 = new("Hey you!", alternates: new[] { "Howdy!" });
    private static readonly Models.TiVoCommand Command2 = new("Test Two");

    private readonly Models.Listening ViewModel = new("MOCKGROUP");
    private readonly Models.RemoteLayoutElement RootLayout =
        new Models.LayoutGroup("COMMANDS", new List<Models.RemoteLayoutElement>
        {
            Command1,
            Command2
        });

    private SpeechController CreateSut() => new(
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
            .Returns(SpeechSettings)
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
    public void SpeechController_OnConstruction_InitializesViewModel()
    {
        // Arrange
        ViewModel.IsListening = true;
        ViewModel.StatusMessage = "Status message was not changed";

        MockDefinition
            .SetupGet(x => x.RemoteRoot)
            .Verifiable(Times.Never);

        // Act
        ISpeechController sut = CreateSut();

        // Assert
        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_WaitingForActivation, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void SpeechController_OnErrorDuringInitialization_LogsError()
    {
        // Arrange
        ISpeechController sut = CreateSut();

        Exception exception = new DataMisalignedException();
        MockDefinition
            .SetupGet(x => x.RemoteRoot)
            .Throws(exception);

        // Act
        sut.Start();

        // Assert
        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechController_ErrorDuringStartup, exception));

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ListeningSystemFailed, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void SpeechController_Start_StartsListeningForAttention()
    {
        // Arrange
        ISpeechController sut = CreateSut();

        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        // Act
        sut.Start();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.SpeechController_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void SpeechController_OnAttention_StartsListeningForCommands()
    {
        // Arrange
        ISpeechController sut = CreateSut();

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

        sut.Start();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.SpeechController_ListenForAttention,
            LoggingMessages.SpeechController_ListenForCommands);

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void SpeechController_OnFirstCommand_AnnouncesAndExecutesCommand()
    {
        // Arrange
        ISpeechController sut = CreateSut();

        Mock<IRecognitionResult> result = new();
        result
            .SetupGet(x => x.Text)
            .Returns(Command1.Name);

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

        sut.Start();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.SpeechController_ListenForAttention,
            LoggingMessages.SpeechController_ListenForCommands,
            string.Format(LoggingMessages.SpeechController_Recognized, Command1.Name),
            string.Format(LoggingMessages.SpeechController_Executing, Command1.Name));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void SpeechController_OnCompletedFirstCommand_LogsExecutedCommand()
    {
        // Arrange
        ISpeechController sut = CreateSut();

        Mock<IRecognitionResult> result1 = new();
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

        sut.Start();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.SpeechController_ListenForAttention,
            LoggingMessages.SpeechController_ListenForCommands,
            string.Format(LoggingMessages.SpeechController_Recognized, Command1.Name),
            string.Format(LoggingMessages.SpeechController_Executing, Command1.Name),
            string.Format(LoggingMessages.SpeechController_Executed, Command1.Name));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void SpeechController_UsingAlternatePhrases_ExecutesCommandTwice()
    {
        // Arrange
        ISpeechController sut = CreateSut();

        Mock<IRecognitionResult> result1 = new();
        result1
            .SetupGet(x => x.Text)
            .Returns(Command1.Name);
        Mock<IRecognitionResult> result2 = new();
        result2
            .SetupGet(x => x.Text)
            .Returns(Command1.Alternates[0]);

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)
            .Verifiable(Times.Once);
        MockRecognition
            .Setup(x => x.ListenForCommands(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerate(false, result1.Object, result2.Object))
            .Verifiable(Times.Once);

        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_ImListening))
            .Verifiable(Times.Once);
        MockSynthesis
            .Setup(x => x.Say(Phrases.Speech_Sending(Command1.Name)))
            .Verifiable(Times.Exactly(2));

        MockExecution
            .Setup(x => x.ExecuteAsync(Command1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));

        sut.Start();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.SpeechController_ListenForAttention,
            LoggingMessages.SpeechController_ListenForCommands,
            string.Format(LoggingMessages.SpeechController_Recognized, Command1.Name),
            string.Format(LoggingMessages.SpeechController_Executing, Command1.Name),
            string.Format(LoggingMessages.SpeechController_Executed, Command1.Name),
            string.Format(LoggingMessages.SpeechController_Recognized, Command1.Alternates[0]),
            string.Format(LoggingMessages.SpeechController_Executing, Command1.Name),
            string.Format(LoggingMessages.SpeechController_Executed, Command1.Name));

        Assert.AreEqual(true, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ImListening, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void SpeechController_OnAttentionAndStopListening_StartsListeningForAttentionAgain()
    {
        // Arrange
        ISpeechController sut = CreateSut();

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

        sut.Start();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.SpeechController_ListenForAttention,
            LoggingMessages.SpeechController_ListenForCommands,
            LoggingMessages.SpeechController_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void SpeechController_OnAttentionAndStopListening_StopsAndStartsListeningForCommandsAgain()
    {
        // Arrange
        ISpeechController sut = CreateSut();

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

        sut.Start();
        tcs.SetResult();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.SpeechController_ListenForAttention,
            LoggingMessages.SpeechController_ListenForCommands,
            LoggingMessages.SpeechController_ListenForAttention,
            LoggingMessages.SpeechController_ListenForCommands,
            LoggingMessages.SpeechController_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void SpeechController_OnError_RestartsListeningForAttention()
    {
        // Arrange
        ISpeechController sut = CreateSut();

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

        sut.Start();

        // Act
        tcs.SetResult();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.SpeechController_ListenForAttention,
            LoggingMessages.SpeechController_ListenForCommands,
            string.Format(LoggingMessages.SpeechController_Error, exception),
            string.Format(LoggingMessages.SpeechController_Retrying, 1),
            LoggingMessages.SpeechController_ListenForAttention);

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ListeningForAttention, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    [Timeout(1000)]
    public void SpeechController_OnRepeatedErrors_StopsRestartingAfterErrorLimit()
    {
        // Arrange
        ISpeechController sut = CreateSut();

        AccessViolationException exception = new AccessViolationException("Whoopsie!");
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Throws(exception)
            .Verifiable(Times.Exactly(SpeechSettings.ErrorRetryLimit));

        // Act
        sut.Start();

        // Assert
        string expectedErrorMessage = string.Format(LoggingMessages.SpeechController_Error, exception);
        MockLogger.VerifyMessages(
            LoggingMessages.SpeechController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.SpeechController_Retrying, 1),
            LoggingMessages.SpeechController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.SpeechController_Retrying, 2),
            LoggingMessages.SpeechController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.SpeechController_Retrying, 3),
            LoggingMessages.SpeechController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.SpeechController_Retrying, 4),
            LoggingMessages.SpeechController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.SpeechController_Retrying, 5),
            LoggingMessages.SpeechController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.SpeechController_Retrying, 6),
            LoggingMessages.SpeechController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.SpeechController_Retrying, 7),
            LoggingMessages.SpeechController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.SpeechController_Retrying, 8),
            LoggingMessages.SpeechController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.SpeechController_Retrying, 9),
            LoggingMessages.SpeechController_ListenForAttention,
            expectedErrorMessage,
            string.Format(LoggingMessages.SpeechController_RetryLimitReached, 10));

        Assert.AreEqual(false, ViewModel.IsListening, nameof(ViewModel.IsListening));
        Assert.AreEqual(Phrases.Speech_ListeningSystemFailed, ViewModel.StatusMessage, nameof(ViewModel.StatusMessage));
    }

    [TestMethod]
    public void SpeechController_StopWhileWaitingForAttention_CancelsWaitingForAttention()
    {
        // Arrange
        ISpeechController sut = CreateSut();

        CancellationToken token = default;
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { token = cancel; })
            .Returns(IncompleteTask)
            .Verifiable(Times.Once);

        sut.Start();

        // Act
        sut.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.SpeechController_ListenForAttention,
            LoggingMessages.SpeechController_Stopping);

        Assert.IsTrue(token.IsCancellationRequested, nameof(token.IsCancellationRequested));
    }

    [TestMethod]
    public void SpeechController_StopCancelsListenForAttention_LogsStopped()
    {
        // Arrange
        ISpeechController sut = CreateSut();

        TaskCompletionSource tcs = new();
        MockRecognition
            .Setup(x => x.ListenForAttention(It.IsAny<CancellationToken>()))
            .Callback(delegate (CancellationToken cancel) { cancel.Register(tcs.SetCanceled); })
            .Returns(tcs.Task)
            .Verifiable(Times.Once);

        sut.Start();

        // Act
        sut.Dispose();

        // Assert
        MockLogger.VerifyMessages(
            LoggingMessages.SpeechController_ListenForAttention,
            LoggingMessages.SpeechController_Stopping,
            LoggingMessages.SpeechController_Stopped);
    }

    // TODO:
    //   Additional logging
    //   Stop support
    //   Refactor so it's not a giant awful function

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
}
