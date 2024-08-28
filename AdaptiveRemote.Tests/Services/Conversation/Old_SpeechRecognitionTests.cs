using System.Speech.Recognition;
using AdaptiveRemote.Logging;
using AdaptiveRemote.TestUtilities;
using Microsoft.Extensions.Options;
using Moq;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class Old_SpeechRecognitionTests
{
    private static readonly TimeSpan ResultTimeout = TimeSpan.FromMilliseconds(100);

    private readonly MockLogger<Old_SpeechRecognition> MockLogger = new();
    private readonly Mock<ISpeechRecognitionEngine> MockEngine = new();
    private readonly Mock<IGrammarProvider> MockGrammarProvider = new();
    private readonly Mock<IListeningController> MockListening = new();
    private readonly Mock<IDisposable> MockListenDisposable = new();
    private readonly MockOptions<ConversationSettings> MockOptions = new();

    private readonly Grammar MockAttentionGrammar = new(new GrammarBuilder("Attention")) { Name = nameof(MockAttentionGrammar) };
    private readonly Grammar MockCommandsGrammar = new(new GrammarBuilder("Commands")) { Name = nameof(MockCommandsGrammar) };
    private readonly Grammar MockYesNoGrammar = new(new GrammarBuilder("YesNo")) { Name = nameof(MockYesNoGrammar) };

    public TestContext? TestContext { get; set; }

    private Old_ISpeechRecognition CreateSut() => new Old_SpeechRecognition(MockOptions, MockEngine.Object, MockListening.Object, MockGrammarProvider.Object, MockLogger);

    private static IRecognizedSpeech CreateMockResult(params string[] semanticValues)
    {
        string? nullValue;

        Mock<IRecognizedSpeech> mockResult = new();
        mockResult
            .Setup(x => x.ContainsSemanticValue(It.IsAny<string>()))
            .Returns(false);
        mockResult
            .Setup(x => x.TryGetSemanticValue(It.IsAny<string>(), out nullValue))
            .Returns(false);

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

        return mockResult.Object;
    }

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

        MockListening
            .Setup(x => x.Listen())
            .Verifiable(Times.Never);
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Never);

        MockLogger.OutputWriter = TestContext;
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockEngine.Verify();
        MockGrammarProvider.Verify();
        MockListening.Verify();
        MockListenDisposable.Verify();
    }

    private static string Expected_GrammarEnabled(Grammar grammar)
        => $"Information[305]: {string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, grammar.Name, true)}";
    private static string Expected_GrammarDisabled(Grammar grammar)
        => $"Information[305]: {string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, grammar.Name, false)}";
    private static string Expected_FailedToLoad(Grammar grammar, Exception error)
        => $"Warning[309]: {string.Format(LoggingMessages.SpeechRecognition_GrammarFailedToLoad, grammar.Name, $"{error.GetType().FullName}: {error.Message}")}";
    private static string Expected_FailedToUnload(Grammar grammar, Exception error)
        => $"Warning[310]: {string.Format(LoggingMessages.SpeechRecognition_GrammarFailedToUnload, grammar.Name, $"{error.GetType().FullName}: {error.Message}")}";
    private static string Expected_RecognitionError(string errorMessage)
        => $"Error[302]: {string.Format(LoggingMessages.SpeechRecognition_RecognitionError, errorMessage)}";
    private static string Expected_ErrorInListenForAttention(Exception error)
        => $"Error[304]: {string.Format(LoggingMessages.SpeechRecognition_ErrorInListeningMethod, nameof(Old_ISpeechRecognition.ListenForAttentionAsync), $"{error.GetType().FullName}: {error.Message}")}";
    private static string Expected_ErrorInListenForCommands(Exception error)
        => $"Error[304]: {string.Format(LoggingMessages.SpeechRecognition_ErrorInListeningMethod, nameof(Old_ISpeechRecognition.ListenForCommandsAsync), $"{error.GetType().FullName}: {error.Message}")}";
    private static string Expected_ErrorInListenForYesNo(Exception error)
        => $"Error[304]: {string.Format(LoggingMessages.SpeechRecognition_ErrorInListeningMethod, nameof(Old_ISpeechRecognition.ListenForYesNoAsync), $"{error.GetType().FullName}: {error.Message}")}";
    private static string Expected_CancelledListenForAttention
        => $"Information[303]: {string.Format(LoggingMessages.SpeechRecognition_CancelledListeningMethod, nameof(Old_ISpeechRecognition.ListenForAttentionAsync))}";
    private static string Expected_CancelledListenForCommands
        => $"Information[303]: {string.Format(LoggingMessages.SpeechRecognition_CancelledListeningMethod, nameof(Old_ISpeechRecognition.ListenForCommandsAsync))}";
    private static string Expected_CancelledListenForYesNo
        => $"Information[303]: {string.Format(LoggingMessages.SpeechRecognition_CancelledListeningMethod, nameof(Old_ISpeechRecognition.ListenForYesNoAsync))}";
    private static string Expected_ListenForAttentionAlreadyInProgress
        => $"Error[308]: {string.Format(LoggingMessages.SpeechRecognition_ListeningMethodAlreadyInProgress, nameof(Old_ISpeechRecognition.ListenForAttentionAsync))}";
    private static string Expected_ListenForCommandsAlreadyInProgress
        => $"Error[308]: {string.Format(LoggingMessages.SpeechRecognition_ListeningMethodAlreadyInProgress, nameof(Old_ISpeechRecognition.ListenForCommandsAsync))}";
    private static string Expected_ListenForYesNoAlreadyInProgress
        => $"Error[308]: {string.Format(LoggingMessages.SpeechRecognition_ListeningMethodAlreadyInProgress, nameof(Old_ISpeechRecognition.ListenForYesNoAsync))}";

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
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = sut.ListenForAttentionAsync(CancellationToken.None);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_ReturnsFaultedOnRecognitionError()
    {
        // Arrange
        const string expectedErrorMessage = "What just happened?";
        RecognitionErrorException expectedException = new RecognitionErrorException(expectedErrorMessage);

        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
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
            Expected_RecognitionError(expectedErrorMessage),
            Expected_ErrorInListenForAttention(expectedException),
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_StopsWaitingWhenCancelled()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
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
            Expected_CancelledListenForAttention,
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_ThrowsIfAlreadyWaitingForAttention()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
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
            Expected_ListenForAttentionAlreadyInProgress);
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_CompletesAndStopsListeningWhenAttentionPhraseDetected()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        Task resultTask = sut.ListenForAttentionAsync(CancellationToken.None);

        IRecognizedSpeech result = CreateMockResult("system=STARTLISTENING");

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognizedSpeechEventArgs(result));

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_CanCallAgainAfterComplete()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Exactly(2));
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        _ = sut.ListenForAttentionAsync(CancellationToken.None);

        IRecognizedSpeech result = CreateMockResult("system=STARTLISTENING");

        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognizedSpeechEventArgs(result));

        // Act
        Task resultTask = sut.ListenForAttentionAsync(CancellationToken.None);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarDisabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_SecondEventDoesNothing()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        Task resultTask = sut.ListenForAttentionAsync(CancellationToken.None);

        IRecognizedSpeech result = CreateMockResult("system=STARTLISTENING");

        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognizedSpeechEventArgs(result));

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognizedSpeechEventArgs(result));

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttentionAsync_StopsListeningOnError()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        InvalidOperationException expectedException = new InvalidOperationException();
        MockListening
            .Setup(x => x.Listen())
            .Throws(expectedException)
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
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_WaitsForYesOrNo()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);

        // Act
        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_ReturnsFaultedOnRecognitionError()
    {
        // Arrange
        const string expectedErrorMessage = "What just happened?";
        RecognitionErrorException expectedException = new RecognitionErrorException(expectedErrorMessage);

        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
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
            Expected_RecognitionError(expectedErrorMessage),
            Expected_ErrorInListenForYesNo(expectedException),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_StopsWaitingWhenCancelled()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
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
            Expected_CancelledListenForYesNo,
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_ThrowsIfAlreadyWaitingForYesNo()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
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
            Expected_ListenForYesNoAlreadyInProgress);
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_CompletesAndStopsListeningWhenYesDetected()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);

        IRecognizedSpeech result = CreateMockResult("system=YES");

        RecognizedSpeechEventArgs eventArgs = new RecognizedSpeechEventArgs(result);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.ResultEquals(resultTask, true, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_CompletesAndStopsListeningWhenNoDetected()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);

        IRecognizedSpeech result = CreateMockResult("system=NO");

        RecognizedSpeechEventArgs eventArgs = new RecognizedSpeechEventArgs(result);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.ResultEquals(resultTask, false, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_DoesNotCompleteWhenYesOrNoNotDetected()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);

        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);

        IRecognizedSpeech result = CreateMockResult("system=OTHER");

        RecognizedSpeechEventArgs eventArgs = new RecognizedSpeechEventArgs(result);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_CanCallAgainAfterComplete()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Exactly(2));
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        _ = sut.ListenForYesNoAsync(CancellationToken.None);

        IRecognizedSpeech result = CreateMockResult("system=YES");
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognizedSpeechEventArgs(result));

        // Act
        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_GrammarDisabled(MockYesNoGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_SecondAttemptDoesNothing()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        IRecognizedSpeech result = CreateMockResult("system=NO");

        Task<bool> resultTask = sut.ListenForYesNoAsync(CancellationToken.None);
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognizedSpeechEventArgs(result));

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognizedSpeechEventArgs(result));

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNoAsync_StopsListeningOnError()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        InvalidOperationException expectedException = new InvalidOperationException();
        MockListening
            .Setup(x => x.Listen())
            .Throws(expectedException)
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
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_MoveNextAsync_WaitsForCommand()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognizedSpeech> resultIter = resultEnum.GetAsyncEnumerator();

        // Act
        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        // Assert
        TaskAssert.IsNotComplete(resultTask, ResultTimeout, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_ReturnsFaultedOnRecognitionError()
    {
        // Arrange
        const string expectedErrorMessage = "What just happened?";
        RecognitionErrorException expectedException = new RecognitionErrorException(expectedErrorMessage);

        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognizedSpeech> resultIter = resultEnum.GetAsyncEnumerator();

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
            Expected_RecognitionError(expectedErrorMessage),
            Expected_ErrorInListenForCommands(expectedException),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_StopsWaitingWhenCancelled()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        CancellationTokenSource cts = new();

        IAsyncEnumerable<IRecognizedSpeech> resultEnum = sut.ListenForCommandsAsync(cts.Token);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognizedSpeech> resultIter = resultEnum.GetAsyncEnumerator();

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
            Expected_CancelledListenForCommands,
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_ThrowsIfAlreadyWaitingForCommands()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
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
            Expected_ListenForCommandsAlreadyInProgress);
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_MoveNextAsync_CompletesWhenCommandDetected()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognizedSpeech> resultIter = resultEnum.GetAsyncEnumerator();
        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        IRecognizedSpeech result = CreateMockResult("command=UP");
        RecognizedSpeechEventArgs eventArgs = new RecognizedSpeechEventArgs(result);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.ResultEquals(resultTask, true, ResultTimeout, nameof(resultTask));
        Assert.AreSame(result, resultIter.Current, nameof(resultIter) + ".Current");
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_DoesNotCompleteWhenNonCommandDetected()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognizedSpeech> resultIter = resultEnum.GetAsyncEnumerator();

        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        IRecognizedSpeech result = CreateMockResult("system=OTHER");

        RecognizedSpeechEventArgs eventArgs = new RecognizedSpeechEventArgs(result);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.IsNotComplete(resultTask, ResultTimeout, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_MoveNextAsync_CompletesWhenStopListeningDetected()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognizedSpeech> resultIter = resultEnum.GetAsyncEnumerator();

        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        IRecognizedSpeech result = CreateMockResult("system=STOPLISTENING");

        RecognizedSpeechEventArgs eventArgs = new RecognizedSpeechEventArgs(result);

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.ResultEquals(resultTask, false, ResultTimeout, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_CanCallAgainAfterComplete()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Exactly(2));
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognizedSpeech> resultIter = resultEnum.GetAsyncEnumerator();

        ValueTask<bool> firstTask = resultIter.MoveNextAsync();

        IRecognizedSpeech result = CreateMockResult("system=STOPLISTENING");

        RecognizedSpeechEventArgs eventArgs = new RecognizedSpeechEventArgs(result);
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
            Expected_GrammarDisabled(MockCommandsGrammar),
            Expected_GrammarEnabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_SecondEventIsAlsoReturned()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognizedSpeech> resultIter = resultEnum.GetAsyncEnumerator();
        ValueTask<bool> previousTask = resultIter.MoveNextAsync();

        IRecognizedSpeech result = CreateMockResult("command=UP");
        RecognizedSpeechEventArgs eventArgs = new RecognizedSpeechEventArgs(result);

        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);
        TaskAssert.IsComplete(previousTask, ResultTimeout, nameof(previousTask));

        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);

        // Assert
        TaskAssert.IsComplete(resultTask, ResultTimeout, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommandsAsync_StopsListeningOnError()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        InvalidOperationException expectedException = new InvalidOperationException();
        MockListening
            .Setup(x => x.Listen())
            .Throws(expectedException)
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognizedSpeech> resultIter = resultEnum.GetAsyncEnumerator();

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
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    [Timeout(1000)]
    public async Task SpeechRecognition_ListenForCommandsAsync_BuffersDefaultNumberOfCommands()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        const int extraCommandsCount = 3;

        for (int i = 0; i < MockOptions.Value.CommandBufferSize + extraCommandsCount; i++)
        {
            IRecognizedSpeech result = CreateMockResult($"command=Up{i}");
            EventArgs eventArgs = new RecognizedSpeechEventArgs(result);
            MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);
        }

        IRecognizedSpeech stopListingResult = CreateMockResult("system=STOPLISTENING");
        EventArgs stopListeningEventArgs = new RecognizedSpeechEventArgs(stopListingResult);
        MockEngine.Raise(x => x.SpeechRecognized -= null, stopListeningEventArgs);

        int resultCount = 0;

        // Act
        await foreach (IRecognizedSpeech result in resultEnum)
        {
            Assert.IsTrue(result.TryGetSemanticValue("command", out string? actualValue), "Did not find expected semantic value for 'command'");
            for (int i = 0; i < extraCommandsCount; i++)
            {
                Assert.AreNotEqual($"Up{i}", actualValue, "Did not expect to see one of the first {0} commands, since the buffer should discard oldest", extraCommandsCount);
            }

            resultCount++;
        }

        // Assert
        Assert.AreEqual(MockOptions.Value.CommandBufferSize, resultCount);

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    [Timeout(1000)]
    public async Task SpeechRecognition_ListenForCommandsAsync_BuffersConfiguredNumberOfCommands()
    {
        // Arrange
        MockOptions.Value.CommandBufferSize = 10;

        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> resultEnum = sut.ListenForCommandsAsync(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        const int extraCommandsCount = 3;

        for (int i = 0; i < MockOptions.Value.CommandBufferSize + extraCommandsCount; i++)
        {
            IRecognizedSpeech result = CreateMockResult($"command=Up{i}");
            EventArgs eventArgs = new RecognizedSpeechEventArgs(result);
            MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);
        }

        IRecognizedSpeech stopListingResult = CreateMockResult("system=STOPLISTENING");
        EventArgs stopListeningEventArgs = new RecognizedSpeechEventArgs(stopListingResult);
        MockEngine.Raise(x => x.SpeechRecognized -= null, stopListeningEventArgs);

        int resultCount = 0;

        // Act
        await foreach (IRecognizedSpeech result in resultEnum)
        {
            Assert.IsTrue(result.TryGetSemanticValue("command", out string? actualValue), "Did not find expected semantic value for 'command'");
            for (int i = 0; i < extraCommandsCount; i++)
            {
                Assert.AreNotEqual($"Up{i}", actualValue, "Did not expect to see one of the first {0} commands, since the buffer should discard oldest", extraCommandsCount);
            }

            resultCount++;
        }

        // Assert
        Assert.AreEqual(MockOptions.Value.CommandBufferSize, resultCount);

        MockLogger.VerifyMessages(
            Expected_GrammarEnabled(MockCommandsGrammar),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_StartsListeningOnlyOnce()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Exactly(3));

        IAsyncEnumerable<IRecognizedSpeech> commandEnum = sut.ListenForCommandsAsync(CancellationToken.None);

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
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesListenForAttention()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Exactly(3));
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> commandEnum = sut.ListenForCommandsAsync(CancellationToken.None);

        Task attentionTask = sut.ListenForAttentionAsync(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNoAsync(CancellationToken.None);
        ValueTask<bool> commandTask = commandEnum.GetAsyncEnumerator().MoveNextAsync();

        IRecognizedSpeech result = CreateMockResult("system=STARTLISTENING");
        RecognizedSpeechEventArgs eventArgs = new(result);

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
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesListenForYesNo()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Exactly(3));
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> commandEnum = sut.ListenForCommandsAsync(CancellationToken.None);

        Task attentionTask = sut.ListenForAttentionAsync(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNoAsync(CancellationToken.None);
        ValueTask<bool> commandTask = commandEnum.GetAsyncEnumerator().MoveNextAsync();

        IRecognizedSpeech result = CreateMockResult("system=YES");
        RecognizedSpeechEventArgs eventArgs = new(result);

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
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesListenForCommands_MoveNextAsync()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Exactly(3));

        IAsyncEnumerable<IRecognizedSpeech> commandEnum = sut.ListenForCommandsAsync(CancellationToken.None);

        Task attentionTask = sut.ListenForAttentionAsync(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNoAsync(CancellationToken.None);
        ValueTask<bool> commandTask = commandEnum.GetAsyncEnumerator().MoveNextAsync();

        IRecognizedSpeech result = CreateMockResult("command=DOWN");
        RecognizedSpeechEventArgs eventArgs = new(result);

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
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesListenForCommands()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Exactly(3));
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> commandEnum = sut.ListenForCommandsAsync(CancellationToken.None);

        Task attentionTask = sut.ListenForAttentionAsync(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNoAsync(CancellationToken.None);
        ValueTask<bool> commandTask = commandEnum.GetAsyncEnumerator().MoveNextAsync();

        IRecognizedSpeech result = CreateMockResult("system=STOPLISTENING");
        RecognizedSpeechEventArgs eventArgs = new(result);

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
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesAllListenersAndStopsListening()
    {
        // Arrange
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Exactly(3));
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Exactly(3));

        IAsyncEnumerable<IRecognizedSpeech> commandEnum = sut.ListenForCommandsAsync(CancellationToken.None);

        Task attentionTask = sut.ListenForAttentionAsync(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNoAsync(CancellationToken.None);
        ValueTask<bool> commandTask = commandEnum.GetAsyncEnumerator().MoveNextAsync();

        IRecognizedSpeech commandsResult = CreateMockResult("system=STOPLISTENING");
        RecognizedSpeechEventArgs commandsEventArgs = new(commandsResult);

        IRecognizedSpeech attentionResult = CreateMockResult("system=STARTLISTENING");
        RecognizedSpeechEventArgs attentionEventArgs = new(attentionResult);

        IRecognizedSpeech yesNoResult = CreateMockResult("system=NO");
        RecognizedSpeechEventArgs yesNoEventArgs = new(yesNoResult);

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
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_GrammarDisabled(MockCommandsGrammar),
            Expected_GrammarDisabled(MockAttentionGrammar),
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_CancelsListenForAttentionAndUnloadsGrammars()
    {
        // Arramge
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
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
            Expected_CancelledListenForAttention,
            Expected_GrammarDisabled(MockAttentionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_UnloadsAllGrammarsIfUnloadAttentionGrammarFails()
    {
        // Arramge
        Old_ISpeechRecognition sut = CreateSut();

        Exception expectedException = new InvalidOperationException("Whoops no unload");

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
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
            Expected_CancelledListenForAttention,
            Expected_GrammarDisabled(MockAttentionGrammar),
            Expected_FailedToUnload(MockAttentionGrammar, expectedException));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_CancelsListenForYesNoAndUnloadsGrammars()
    {
        // Arramge
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
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
            Expected_CancelledListenForYesNo,
            Expected_GrammarDisabled(MockYesNoGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_UnloadsAllGrammarsIfUnloadYesNoGrammarFails()
    {
        // Arramge
        Old_ISpeechRecognition sut = CreateSut();

        Exception expectedException = new InvalidOperationException("Whoops no unload");

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
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
            Expected_CancelledListenForYesNo,
            Expected_GrammarDisabled(MockYesNoGrammar),
            Expected_FailedToUnload(MockYesNoGrammar, expectedException));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_CancelsListenForCommandsAndUnloadsGrammars()
    {
        // Arramge
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
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

        IAsyncEnumerable<IRecognizedSpeech> commandsEnum = sut.ListenForCommandsAsync(default);
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
            Expected_CancelledListenForCommands,
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_UnloadsAllGrammarsIfUnloadCommandsGrammarFails()
    {
        // Arramge
        Old_ISpeechRecognition sut = CreateSut();

        Exception expectedException = new InvalidOperationException("Whoops no unload");

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
        MockListenDisposable
            .Setup(x => x.Dispose())
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

        IAsyncEnumerable<IRecognizedSpeech> commandsEnum = sut.ListenForCommandsAsync(default);
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
            Expected_CancelledListenForCommands,
            Expected_GrammarDisabled(MockCommandsGrammar),
            Expected_FailedToUnload(MockCommandsGrammar, expectedException));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_CancelsAllListenMethodsAndUnloadsGrammars()
    {
        // Arramge
        Old_ISpeechRecognition sut = CreateSut();

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Exactly(3));
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Exactly(3));
        MockEngine
            .Setup(x => x.UnloadGrammar(MockAttentionGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockYesNoGrammar))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.UnloadGrammar(MockCommandsGrammar))
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognizedSpeech> commandsEnum = sut.ListenForCommandsAsync(default);
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
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_CancelledListenForYesNo,
            Expected_GrammarDisabled(MockYesNoGrammar),
            Expected_CancelledListenForAttention,
            Expected_GrammarDisabled(MockAttentionGrammar),
            Expected_CancelledListenForCommands,
            Expected_GrammarDisabled(MockCommandsGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_Dispose_UnloadsAllGrammarsIfAllUnloadGrammarCallsFail()
    {
        // Arramge
        Old_ISpeechRecognition sut = CreateSut();

        Exception expectedException = new InvalidOperationException("Whoops no unload");

        MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Exactly(3));
        MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Exactly(3));
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

        IAsyncEnumerable<IRecognizedSpeech> commandsEnum = sut.ListenForCommandsAsync(default);
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
            Expected_GrammarEnabled(MockAttentionGrammar),
            Expected_GrammarEnabled(MockYesNoGrammar),
            Expected_CancelledListenForYesNo,
            Expected_GrammarDisabled(MockYesNoGrammar),
            Expected_CancelledListenForAttention,
            Expected_GrammarDisabled(MockAttentionGrammar),
            Expected_CancelledListenForCommands,
            Expected_GrammarDisabled(MockCommandsGrammar),
            Expected_FailedToUnload(MockAttentionGrammar, expectedException),
            Expected_FailedToUnload(MockYesNoGrammar, expectedException),
            Expected_FailedToUnload(MockCommandsGrammar, expectedException));
    }
}
