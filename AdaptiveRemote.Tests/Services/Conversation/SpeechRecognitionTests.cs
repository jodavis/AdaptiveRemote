using System.Speech.Recognition;
using AdaptiveRemote.Logging;
using AdaptiveRemote.TestUtilities;
using Microsoft.Extensions.Options;
using Moq;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class SpeechRecognitionTests
{
    private static readonly TimeSpan ResultTimeout = TimeSpan.FromMilliseconds(100);

    private readonly MockLogger<SpeechRecognition> MockLogger = new();
    private readonly Mock<ISpeechRecognitionEngine> MockEngine = new();
    private readonly Mock<IGrammarProvider> MockGrammarProvider = new();
    private readonly Mock<IOptionsSnapshot<ConversationSettings>> MockSettings = new();
    private readonly ConversationSettings Settings = new();

    private readonly Grammar MockAttentionGrammar = new(new GrammarBuilder("Attention")) { Name = nameof(MockAttentionGrammar) };
    private readonly Grammar MockCommandsGrammar = new(new GrammarBuilder("Commands")) { Name = nameof(MockCommandsGrammar) };
    private readonly Grammar MockYesNoGrammar = new(new GrammarBuilder("YesNo")) { Name = nameof(MockYesNoGrammar) };

    private ISpeechRecognition CreateSut() => new SpeechRecognition(MockSettings.Object, MockEngine.Object, MockGrammarProvider.Object, MockLogger);

    public TestContext? TestContext { get; set; }

    [TestInitialize]
    public void SetupMocks()
    {
        MockAttentionGrammar.Enabled = true;
        MockGrammarProvider
            .Setup(x => x.LoadAttentionGrammar())
            .Returns(MockAttentionGrammar)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.LoadGrammar(MockAttentionGrammar))
            .Callback(() => Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled"))
            .Verifiable(Times.Once);

        MockCommandsGrammar.Enabled = true;
        MockGrammarProvider
            .Setup(x => x.LoadCommandsGrammar())
            .Returns(MockCommandsGrammar)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.LoadGrammar(MockCommandsGrammar))
            .Callback(() => Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled"))
            .Verifiable(Times.Once);

        MockYesNoGrammar.Enabled = true;
        MockGrammarProvider
            .Setup(x => x.LoadYesNoGrammar())
            .Returns(MockYesNoGrammar)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.LoadGrammar(MockYesNoGrammar))
            .Callback(() => Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled"))
            .Verifiable(Times.Once);

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Never);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Never);

        MockSettings
            .SetupGet(x => x.Value)
            .Returns(Settings)
            .Verifiable(Times.Once);

        MockLogger.OutputWriter = TestContext;
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockEngine.Verify();
        MockGrammarProvider.Verify();
    }

    private static string Expected_GrammarEnabled(Grammar grammar)
        => $"Information[305]: {string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, grammar.Name, true)}";
    private static string Expected_GrammarDisabled(Grammar grammar)
        => $"Information[305]: {string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, grammar.Name, false)}";
    private static string Expected_FailedToLoad(Grammar grammar, Exception error)
        => $"Warning[309]: {string.Format(LoggingMessages.SpeechRecognition_GrammarFailedToLoad, grammar.Name, $"{error.GetType().FullName}: {error.Message}")}";
    private static string Expected_FailedToUnload(Grammar grammar, Exception error)
        => $"Warning[310]: {string.Format(LoggingMessages.SpeechRecognition_GrammarFailedToUnload, grammar.Name, $"{error.GetType().FullName}: {error.Message}")}";
    private static string Expected_Listening(bool listening)
        => $"Information[306]: {string.Format(LoggingMessages.SpeechRecognition_Listening, listening)}";
    private static string Expected_RecognitionError(string errorMessage)
        => $"Error[302]: {string.Format(LoggingMessages.SpeechRecognition_RecognitionError, errorMessage)}";
    private static string Expected_ErrorInListenForAttention(Exception error)
        => $"Error[304]: {string.Format(LoggingMessages.SpeechRecognition_ErrorInListeningMethod, nameof(ISpeechRecognition.ListenForAttentionAsync), $"{error.GetType().FullName}: {error.Message}")}";
    private static string Expected_ErrorInListenForCommands(Exception error)
        => $"Error[304]: {string.Format(LoggingMessages.SpeechRecognition_ErrorInListeningMethod, nameof(ISpeechRecognition.ListenForCommandsAsync), $"{error.GetType().FullName}: {error.Message}")}";
    private static string Expected_ErrorInListenForYesNo(Exception error)
        => $"Error[304]: {string.Format(LoggingMessages.SpeechRecognition_ErrorInListeningMethod, nameof(ISpeechRecognition.ListenForYesNoAsync), $"{error.GetType().FullName}: {error.Message}")}";
    private static string Expected_CancelledListenForAttention
        => $"Information[303]: {string.Format(LoggingMessages.SpeechRecognition_CancelledListeningMethod, nameof(ISpeechRecognition.ListenForAttentionAsync))}";
    private static string Expected_CancelledListenForCommands
        => $"Information[303]: {string.Format(LoggingMessages.SpeechRecognition_CancelledListeningMethod, nameof(ISpeechRecognition.ListenForCommandsAsync))}";
    private static string Expected_CancelledListenForYesNo
        => $"Information[303]: {string.Format(LoggingMessages.SpeechRecognition_CancelledListeningMethod, nameof(ISpeechRecognition.ListenForYesNoAsync))}";
    private static string Expected_ListenForAttentionAlreadyInProgress
        => $"Error[308]: {string.Format(LoggingMessages.SpeechRecognition_ListeningMethodAlreadyInProgress, nameof(ISpeechRecognition.ListenForAttentionAsync))}";
    private static string Expected_ListenForCommandsAlreadyInProgress
        => $"Error[308]: {string.Format(LoggingMessages.SpeechRecognition_ListeningMethodAlreadyInProgress, nameof(ISpeechRecognition.ListenForCommandsAsync))}";
    private static string Expected_ListenForYesNoAlreadyInProgress
        => $"Error[308]: {string.Format(LoggingMessages.SpeechRecognition_ListeningMethodAlreadyInProgress, nameof(ISpeechRecognition.ListenForYesNoAsync))}";

    [TestMethod]
    public void SpeechRecognition_Constructor_LoadsAndDisablesGrammars()
    {
        // Arrange

        // Act
        _ = CreateSut();

        // Assert
        MockLogger.VerifyMessages();
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_WaitsForAttentionPhrase()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        // Act
        Task resultTask = sut.ListenForAttentionAsync(CancellationToken.None);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_Listening(true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_ReturnsFaultedOnRecognitionError()
    {
        // Arrange
        const string expectedErrorMessage = "What just happened?";
        RecognitionErrorException expectedException = new RecognitionErrorException(expectedErrorMessage);

        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        Task resultTask = sut.ListenForAttentionAsync(CancellationToken.None);

        // Act
        MockEngine.Raise(x => x.RecognitionError -= null, new RecognitionErrorEventArgs(expectedErrorMessage));

        // Assert
        TaskAssert.IsFaulted(resultTask, expectedException, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_Listening(true),
            Expected_RecognitionError(expectedErrorMessage),
            Expected_ErrorInListenForAttention(expectedException),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_StopsWaitingWhenCancelled()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        CancellationTokenSource cts = new();
        Task resultTask = sut.ListenForAttentionAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_Listening(true),
            Expected_CancelledListenForAttention,
            Expected_Listening(false),
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_ThrowsIfAlreadyWaitingForAttention()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        _ = sut.ListenForAttentionAsync(CancellationToken.None);

        // Act
        Task resultTask = sut.ListenForAttentionAsync(CancellationToken.None);

        // Assert
        TaskAssert.IsFaulted(resultTask, new InvalidOperationException("ListenForAttentionAsync is already in progress"), nameof(resultTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_Listening(true),
            Expected_ListenForAttentionAlreadyInProgress);
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_CompletesAndStopsListeningWhenAttentionPhraseDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        Task resultTask = sut.ListenForAttentionAsync(CancellationToken.None);

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("STARTLISTENING");

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognitionResultEventArgs(mockResult.Object));

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_Listening(true),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_CanCallAgainAfterComplete()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Exactly(2));
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        _ = sut.ListenForAttentionAsync(CancellationToken.None);

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("STARTLISTENING");

        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognitionResultEventArgs(mockResult.Object));

        // Act
        Task resultTask = sut.ListenForAttentionAsync(CancellationToken.None);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_Listening(true),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_Listening(true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_SecondEventDoesNothing()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        Task resultTask = sut.ListenForAttentionAsync(CancellationToken.None);

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("STARTLISTENING");

        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognitionResultEventArgs(mockResult.Object));

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognitionResultEventArgs(mockResult.Object));

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_Listening(true),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_StopsListeningOnError()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        InvalidOperationException expectedException = new InvalidOperationException();
        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Throws(expectedException)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        // Act
        Task resultTask = sut.ListenForAttentionAsync(CancellationToken.None);

        // Assert
        TaskAssert.IsFaulted(resultTask, expectedException, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_ErrorInListenForAttention(expectedException),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_WaitsForYesOrNo()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        // Act
        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_Listening(true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_ReturnsFaultedOnRecognitionError()
    {
        // Arrange
        const string expectedErrorMessage = "What just happened?";
        RecognitionErrorException expectedException = new RecognitionErrorException(expectedErrorMessage);

        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);

        // Act
        MockEngine.Raise(x => x.RecognitionError -= null, new RecognitionErrorEventArgs(expectedErrorMessage));

        // Assert
        TaskAssert.IsFaulted(resultTask, expectedException, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_Listening(true),
            Expected_RecognitionError(expectedErrorMessage),
            Expected_ErrorInListenForYesNo(expectedException),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_StopsWaitingWhenCancelled()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        CancellationTokenSource cts = new();
        Task<bool> resultTask = sut.ListenForYesNoAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_Listening(true),
            Expected_CancelledListenForYesNo,
            Expected_Listening(false),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_ThrowsIfAlreadyWaitingForYesNo()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        _ = sut.ListenForYesNoAsync(CancellationToken.None);

        // Act
        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);

        // Assert
        TaskAssert.IsFaulted(resultTask, new InvalidOperationException("ListenForYesNoAsync is already in progress"), nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_Listening(true),
            Expected_ListenForYesNoAlreadyInProgress);
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_CompletesAndStopsListeningWhenYesDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("YES");

        RecognitionResultEventArgs eventArgs = new RecognitionResultEventArgs(mockResult.Object);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.ResultEquals(resultTask, true, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_Listening(true),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_CompletesAndStopsListeningWhenNoDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("NO");

        RecognitionResultEventArgs eventArgs = new RecognitionResultEventArgs(mockResult.Object);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.ResultEquals(resultTask, false, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_Listening(true),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_DoesNotCompleteWhenYesOrNoNotDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("OTHER");

        RecognitionResultEventArgs eventArgs = new RecognitionResultEventArgs(mockResult.Object);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_Listening(true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_CanCallAgainAfterComplete()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Exactly(2));
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        _ = sut.ListenForYesNoAsync(CancellationToken.None);

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("YES");
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognitionResultEventArgs(mockResult.Object));

        // Act
        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_Listening(true),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockYesNoGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_Listening(true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_SecondAttemptDoesNothing()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("NO");

        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognitionResultEventArgs(mockResult.Object));

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognitionResultEventArgs(mockResult.Object));

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_Listening(true),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_StopsListeningOnError()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        InvalidOperationException expectedException = new InvalidOperationException();
        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Throws(expectedException)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        // Act
        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);

        // Assert
        TaskAssert.IsFaulted(resultTask, expectedException, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_ErrorInListenForYesNo(expectedException),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_MoveNextAsync_WaitsForCommand()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        // Act
        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        // Assert
        TaskAssert.IsNotComplete(resultTask, ResultTimeout, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_ReturnsFaultedOnRecognitionError()
    {
        // Arrange
        const string expectedErrorMessage = "What just happened?";
        RecognitionErrorException expectedException = new RecognitionErrorException(expectedErrorMessage);

        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        // Act
        MockEngine.Raise(x => x.RecognitionError -= null, new RecognitionErrorEventArgs(expectedErrorMessage));

        // Assert
        TaskAssert.IsFaulted(resultTask, expectedException, ResultTimeout, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_RecognitionError(expectedErrorMessage),
            Expected_ErrorInListenForCommands(expectedException),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_StopsWaitingWhenCancelled()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        CancellationTokenSource cts = new();

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommandsAsync(cts.Token);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsCanceled(resultTask, ResultTimeout, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_CancelledListenForCommands,
            Expected_Listening(false),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_ThrowsIfAlreadyWaitingForCommands()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        _ = sut.ListenForCommandsAsync(CancellationToken.None);

        try
        {
            // Act
            _ = sut.ListenForCommandsAsync(CancellationToken.None);

            // Assert
            Assert.Fail("Expected exception was not thrown");
        }
        catch (InvalidOperationException result)
        {
            Assert.AreEqual("ListenForCommandsAsync is already in progress", result.Message, nameof(result) + ".Message");
        }

        // Assert
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_ListenForCommandsAlreadyInProgress);
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_MoveNextAsync_CompletesWhenCommandDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("Command:UP x3");

        RecognitionResultEventArgs eventArgs = new RecognitionResultEventArgs(mockResult.Object);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.ResultEquals(resultTask, true, ResultTimeout, nameof(resultTask));
        Assert.AreSame(mockResult.Object, resultIter.Current, nameof(resultIter) + ".Current");
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_DoesNotCompleteWhenNonCommandDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("OTHER");

        RecognitionResultEventArgs eventArgs = new RecognitionResultEventArgs(mockResult.Object);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.IsNotComplete(resultTask, ResultTimeout, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_MoveNextAsync_CompletesWhenStopListeningDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("STOPLISTENING");

        RecognitionResultEventArgs eventArgs = new RecognitionResultEventArgs(mockResult.Object);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.ResultEquals(resultTask, false, ResultTimeout, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_CanCallAgainAfterComplete()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Exactly(2));
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        ValueTask<bool> firstTask = resultIter.MoveNextAsync();

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("STOPLISTENING");

        RecognitionResultEventArgs eventArgs = new RecognitionResultEventArgs(mockResult.Object);
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);
        TaskAssert.ResultEquals(firstTask, false, ResultTimeout, nameof(firstTask));

        // Act
        ValueTask<bool> resultTask = sut.ListenForCommandsAsync(CancellationToken.None).GetAsyncEnumerator().MoveNextAsync();

        // Assert
        TaskAssert.IsNotComplete(resultTask, ResultTimeout, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockCommandsGrammar),
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_SecondEventIsAlsoReturned()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("Command:UP");

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        ValueTask<bool> firstTask = resultIter.MoveNextAsync();

        RecognitionResultEventArgs eventArgs = new RecognitionResultEventArgs(mockResult.Object);
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);
        TaskAssert.ResultEquals(firstTask, true, ResultTimeout, nameof(firstTask));

        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognitionResultEventArgs(mockResult.Object));

        // Assert
        TaskAssert.IsComplete(resultTask, ResultTimeout, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_StopsListeningOnError()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        InvalidOperationException expectedException = new InvalidOperationException();
        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Throws(expectedException)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        // Act
        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        // Assert
        TaskAssert.IsFaulted(resultTask, expectedException, ResultTimeout, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_ErrorInListenForCommands(expectedException),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public async Task SpeechRecognition_ListenForCommandsAsync_BuffersDefaultNumberOfCommands()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        const int extraCommandsCount = 3;

        for (int i = 0; i < Settings.CommandBufferSize + extraCommandsCount; i++)
        {
            Mock<IRecognitionResult> mockResult = new();
            mockResult
                .SetupGet(x => x.SemanticMeaning)
                .Returns($"Command:ChannelUp{i}");

            EventArgs eventArgs = new RecognitionResultEventArgs(mockResult.Object);
            MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);
        }

        Mock<IRecognitionResult> mockStopListeningResult = new();
        mockStopListeningResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("STOPLISTENING");

        EventArgs stopListeningEventArgs = new RecognitionResultEventArgs(mockStopListeningResult.Object);
        MockEngine.Raise(x => x.SpeechRecognized -= null, stopListeningEventArgs);

        int resultCount = 0;

        // Act
        await foreach (IRecognitionResult result in resultEnum)
        {
            for (int i = 0; i < extraCommandsCount; i++)
            {
                Assert.AreNotEqual($"Command:ChannelUp{i}", result.SemanticMeaning, "Did not expect to see one of the first {0} commands, since the buffer should discard oldest", extraCommandsCount);
            }

            resultCount++;
        }

        // Assert
        Assert.AreEqual(Settings.CommandBufferSize, resultCount);

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public async Task SpeechRecognition_ListenForCommandsAsync_BuffersConfiguredNumberOfCommands()
    {
        // Arrange
        Settings.CommandBufferSize = 10;

        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        const int extraCommandsCount = 3;

        for (int i = 0; i < Settings.CommandBufferSize + extraCommandsCount; i++)
        {
            Mock<IRecognitionResult> mockResult = new();
            mockResult
                .SetupGet(x => x.SemanticMeaning)
                .Returns($"Command:ChannelUp{i}");

            EventArgs eventArgs = new RecognitionResultEventArgs(mockResult.Object);
            MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);
        }

        Mock<IRecognitionResult> mockStopListeningResult = new();
        mockStopListeningResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("STOPLISTENING");

        EventArgs stopListeningEventArgs = new RecognitionResultEventArgs(mockStopListeningResult.Object);
        MockEngine.Raise(x => x.SpeechRecognized -= null, stopListeningEventArgs);

        int resultCount = 0;

        // Act
        await foreach (IRecognitionResult result in resultEnum)
        {
            for (int i = 0; i < extraCommandsCount; i++)
            {
                Assert.AreNotEqual($"Command:ChannelUp{i}", result.SemanticMeaning, "Did not expect to see one of the first {0} commands, since the buffer should discard oldest", extraCommandsCount);
            }

            resultCount++;
        }

        // Assert
        Assert.AreEqual(Settings.CommandBufferSize, resultCount);

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_StartsListeningOnlyOnce()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandEnum = sut.ListenForCommandsAsync(CancellationToken.None);

        // Act
        Task attentionTask = sut.ListenForAttentionAsync(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNoAsync(CancellationToken.None);
        ValueTask<bool> commandTask = commandEnum.GetAsyncEnumerator().MoveNextAsync();

        // Assert
        TaskAssert.IsNotComplete(attentionTask, nameof(attentionTask));
        TaskAssert.IsNotComplete(yesNoTask, nameof(yesNoTask));
        TaskAssert.IsNotComplete(commandTask, ResultTimeout, nameof(commandTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesListenForAttention()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandEnum = sut.ListenForCommandsAsync(CancellationToken.None);

        Task attentionTask = sut.ListenForAttentionAsync(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNoAsync(CancellationToken.None);
        ValueTask<bool> commandTask = commandEnum.GetAsyncEnumerator().MoveNextAsync();

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("STARTLISTENING");
        RecognitionResultEventArgs eventArgs = new(mockResult.Object);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.IsComplete(attentionTask, nameof(attentionTask));
        TaskAssert.IsNotComplete(yesNoTask, nameof(yesNoTask));
        TaskAssert.IsNotComplete(commandTask, ResultTimeout, nameof(commandTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesListenForYesNo()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandEnum = sut.ListenForCommandsAsync(CancellationToken.None);

        Task attentionTask = sut.ListenForAttentionAsync(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNoAsync(CancellationToken.None);
        ValueTask<bool> commandTask = commandEnum.GetAsyncEnumerator().MoveNextAsync();

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("YES");
        RecognitionResultEventArgs eventArgs = new(mockResult.Object);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.IsNotComplete(attentionTask, nameof(attentionTask));
        TaskAssert.ResultEquals(yesNoTask, true, nameof(yesNoTask));
        TaskAssert.IsNotComplete(commandTask, ResultTimeout, nameof(commandTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesListenForCommands_MoveNextAsync()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandEnum = sut.ListenForCommandsAsync(CancellationToken.None);

        Task attentionTask = sut.ListenForAttentionAsync(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNoAsync(CancellationToken.None);
        ValueTask<bool> commandTask = commandEnum.GetAsyncEnumerator().MoveNextAsync();

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("Command:DOWN");
        RecognitionResultEventArgs eventArgs = new(mockResult.Object);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.IsNotComplete(attentionTask, nameof(attentionTask));
        TaskAssert.IsNotComplete(yesNoTask, nameof(yesNoTask));
        TaskAssert.ResultEquals(commandTask, true, ResultTimeout, nameof(commandTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesListenForCommands()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandEnum = sut.ListenForCommandsAsync(CancellationToken.None);

        Task attentionTask = sut.ListenForAttentionAsync(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNoAsync(CancellationToken.None);
        ValueTask<bool> commandTask = commandEnum.GetAsyncEnumerator().MoveNextAsync();

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("STOPLISTENING");
        RecognitionResultEventArgs eventArgs = new(mockResult.Object);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.IsNotComplete(attentionTask, nameof(attentionTask));
        TaskAssert.IsNotComplete(yesNoTask, nameof(yesNoTask));
        TaskAssert.ResultEquals(commandTask, false, ResultTimeout, nameof(commandTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesAllListenersAndStopsListening()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandEnum = sut.ListenForCommandsAsync(CancellationToken.None);

        Task attentionTask = sut.ListenForAttentionAsync(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNoAsync(CancellationToken.None);
        ValueTask<bool> commandTask = commandEnum.GetAsyncEnumerator().MoveNextAsync();

        Mock<IRecognitionResult> mockCommandsResult = new();
        mockCommandsResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("STOPLISTENING");
        RecognitionResultEventArgs commandsEventArgs = new(mockCommandsResult.Object);

        Mock<IRecognitionResult> mockAttentionResult = new();
        mockAttentionResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("STARTLISTENING");
        RecognitionResultEventArgs attentionEventArgs = new(mockAttentionResult.Object);

        Mock<IRecognitionResult> mockYesNoResult = new();
        mockYesNoResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("NO");
        RecognitionResultEventArgs yesNoEventArgs = new(mockYesNoResult.Object);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, commandsEventArgs);
        MockEngine.Raise(x => x.SpeechRecognized -= null, attentionEventArgs);
        MockEngine.Raise(x => x.SpeechRecognized -= null, yesNoEventArgs);

        // Assert
        TaskAssert.IsComplete(attentionTask, nameof(attentionTask));
        TaskAssert.ResultEquals(yesNoTask, false, nameof(yesNoTask));
        TaskAssert.ResultEquals(commandTask, false, ResultTimeout, nameof(commandTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_GrammarDisabled(MockCommandsGrammar),
            Expected_GrammarDisabled(MockAttentionGrammar),
            Expected_Listening(false),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_CancelsListenForAttentionAndUnloadsGrammars()
    {
        // Arramge
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockAttentionGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockYesNoGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockCommandsGrammar))
            .Verifiable(Times.Once);

        Task attentionResult = sut.ListenForAttentionAsync(default);

        // Act
        ((IDisposable)sut).Dispose();

        // Assert
        TaskAssert.IsCanceled(attentionResult, nameof(attentionResult));

        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_Listening(true),
            Expected_CancelledListenForAttention,
            Expected_Listening(false),
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_UnloadsAllGrammarsIfUnloadAttentionGrammarFails()
    {
        // Arramge
        ISpeechRecognition sut = CreateSut();

        Exception expectedException = new InvalidOperationException("Whoops no unload");

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockAttentionGrammar))
            .Throws(expectedException)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockYesNoGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockCommandsGrammar))
            .Verifiable(Times.Once);

        Task attentionResult = sut.ListenForAttentionAsync(default);

        // Act
        ((IDisposable)sut).Dispose();

        // Assert
        TaskAssert.IsCanceled(attentionResult, nameof(attentionResult));

        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_Listening(true),
            Expected_CancelledListenForAttention,
            Expected_Listening(false),
            Expected_GrammarDisabled(MockAttentionGrammar),
            Expected_FailedToUnload(MockAttentionGrammar, expectedException));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_CancelsListenForYesNoAndUnloadsGrammars()
    {
        // Arramge
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockAttentionGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockYesNoGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockCommandsGrammar))
            .Verifiable(Times.Once);

        Task yesNoResult = sut.ListenForYesNoAsync(default);

        // Act
        ((IDisposable)sut).Dispose();

        // Assert
        TaskAssert.IsCanceled(yesNoResult, nameof(yesNoResult));

        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_Listening(true),
            Expected_CancelledListenForYesNo,
            Expected_Listening(false),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_UnloadsAllGrammarsIfUnloadYesNoGrammarFails()
    {
        // Arramge
        ISpeechRecognition sut = CreateSut();

        Exception expectedException = new InvalidOperationException("Whoops no unload");

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockAttentionGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockYesNoGrammar))
            .Throws(expectedException)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockCommandsGrammar))
            .Verifiable(Times.Once);

        Task yesNoResult = sut.ListenForYesNoAsync(default);

        // Act
        ((IDisposable)sut).Dispose();

        // Assert
        TaskAssert.IsCanceled(yesNoResult, ResultTimeout, nameof(yesNoResult));

        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_Listening(true),
            Expected_CancelledListenForYesNo,
            Expected_Listening(false),
            Expected_GrammarDisabled(MockYesNoGrammar),
            Expected_FailedToUnload(MockYesNoGrammar, expectedException));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_CancelsListenForCommandsAndUnloadsGrammars()
    {
        // Arramge
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockAttentionGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockYesNoGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockCommandsGrammar))
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandsEnum = sut.ListenForCommandsAsync(default);
        ValueTask<bool> commandsResult = commandsEnum.GetAsyncEnumerator().MoveNextAsync();

        // Act
        ((IDisposable)sut).Dispose();

        // Assert
        TaskAssert.IsCanceled(commandsResult, ResultTimeout, nameof(commandsResult));

        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_CancelledListenForCommands,
            Expected_Listening(false),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_UnloadsAllGrammarsIfUnloadCommandsGrammarFails()
    {
        // Arramge
        ISpeechRecognition sut = CreateSut();

        Exception expectedException = new InvalidOperationException("Whoops no unload");

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockAttentionGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockYesNoGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockCommandsGrammar))
            .Throws(expectedException)
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandsEnum = sut.ListenForCommandsAsync(default);
        ValueTask<bool> commandsResult = commandsEnum.GetAsyncEnumerator().MoveNextAsync();

        // Act
        ((IDisposable)sut).Dispose();

        // Assert
        TaskAssert.IsCanceled(commandsResult, ResultTimeout, nameof(commandsResult));

        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_CancelledListenForCommands,
            Expected_Listening(false),
            Expected_GrammarDisabled(MockCommandsGrammar),
            Expected_FailedToUnload(MockCommandsGrammar, expectedException));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_CancelsAllListenMethodsAndUnloadsGrammars()
    {
        // Arramge
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockAttentionGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockYesNoGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockCommandsGrammar))
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandsEnum = sut.ListenForCommandsAsync(default);
        ValueTask<bool> commandsResult = commandsEnum.GetAsyncEnumerator().MoveNextAsync();
        Task attentionResult = sut.ListenForAttentionAsync(default);
        Task yesNoResult = sut.ListenForYesNoAsync(default);

        // Act
        ((IDisposable)sut).Dispose();

        // Assert
        TaskAssert.IsCanceled(commandsResult, ResultTimeout, nameof(commandsResult));
        TaskAssert.IsCanceled(attentionResult, nameof(commandsResult));
        TaskAssert.IsCanceled(yesNoResult, nameof(commandsResult));

        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_CancelledListenForYesNo,
            Expected_GrammarDisabled(MockYesNoGrammar),
            Expected_CancelledListenForAttention,
            Expected_GrammarDisabled(MockAttentionGrammar),
            Expected_CancelledListenForCommands,
            Expected_Listening(false),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_UnloadsAllGrammarsIfAllUnloadGrammarCallsFail()
    {
        // Arramge
        ISpeechRecognition sut = CreateSut();

        Exception expectedException = new InvalidOperationException("Whoops no unload");

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockAttentionGrammar))
            .Throws(expectedException)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockYesNoGrammar))
            .Throws(expectedException)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockCommandsGrammar))
            .Throws(expectedException)
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandsEnum = sut.ListenForCommandsAsync(default);
        ValueTask<bool> commandsResult = commandsEnum.GetAsyncEnumerator().MoveNextAsync();
        Task attentionResult = sut.ListenForAttentionAsync(default);
        Task yesNoResult = sut.ListenForYesNoAsync(default);

        // Act
        ((IDisposable)sut).Dispose();

        // Assert
        TaskAssert.IsCanceled(commandsResult, ResultTimeout, nameof(commandsResult));
        TaskAssert.IsCanceled(attentionResult, nameof(commandsResult));
        TaskAssert.IsCanceled(yesNoResult, nameof(commandsResult));

        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_Listening(true),
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_CancelledListenForYesNo,
            Expected_GrammarDisabled(MockYesNoGrammar),
            Expected_CancelledListenForAttention,
            Expected_GrammarDisabled(MockAttentionGrammar),
            Expected_CancelledListenForCommands,
            Expected_Listening(false),
            Expected_GrammarDisabled(MockCommandsGrammar),
            Expected_FailedToUnload(MockAttentionGrammar, expectedException),
            Expected_FailedToUnload(MockYesNoGrammar, expectedException),
            Expected_FailedToUnload(MockCommandsGrammar, expectedException));
    }
}
