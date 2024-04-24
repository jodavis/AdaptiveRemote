using System.Speech.Recognition;
using AdaptiveRemote.TestUtilities;
using Moq;

namespace AdaptiveRemote.Services.Speech;

[TestClass]
public class SpeechRecognitionTests
{
    private readonly MockLogger<SpeechRecognition> MockLogger = new();
    private readonly Mock<ISpeechRecognitionEngine> MockEngine = new();
    private readonly Mock<IGrammarProvider> MockGrammarProvider = new();

    private readonly Grammar MockAttentionGrammar = new(new GrammarBuilder("Attention")) { Name = nameof(MockAttentionGrammar) };
    private readonly Grammar MockCommandsGrammar = new(new GrammarBuilder("Commands")) { Name = nameof(MockCommandsGrammar) };
    private readonly Grammar MockYesNoGrammar = new(new GrammarBuilder("YesNo")) { Name = nameof(MockYesNoGrammar) };

    private ISpeechRecognition CreateSut() => new SpeechRecognition(MockEngine.Object, MockGrammarProvider.Object, MockLogger);

    public SpeechRecognitionTests()
    {
        MockEngine
            .Setup(x => x.UnloadAllGrammars())
            .Callback(() => MockEngine.Verify(x => x.LoadGrammar(It.IsAny<Grammar>()), Times.Never))
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.SetInputToDefaultAudioDevice())
            .Verifiable(Times.Once);

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
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockEngine.Verify();
        MockGrammarProvider.Verify();
    }

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
    public void SpeechRecognition_ListenForAttention_WaitsForAttentionPhrase()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        // Act
        Task resultTask = sut.ListenForAttention(CancellationToken.None);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttention_StopsWaitingWhenCancelled()
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
        Task resultTask = sut.ListenForAttention(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_CancelledListeningMethod, "ListenForAttention"),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttention_ThrowsIfAlreadyWaitingForAttention()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        _ = sut.ListenForAttention(CancellationToken.None);

        // Act
        Task resultTask = sut.ListenForAttention(CancellationToken.None);

        // Assert
        TaskAssert.IsFaulted(resultTask, new InvalidOperationException("ListenForAttention is already in progress"), nameof(resultTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            "ListenForAttention is already in progress");
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttention_CompletesAndStopsListeningWhenAttentionPhraseDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        Task resultTask = sut.ListenForAttention(CancellationToken.None);

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
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttention_CanCallAgainAfterComplete()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Exactly(2));
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        _ = sut.ListenForAttention(CancellationToken.None);

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("STARTLISTENING");

        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognitionResultEventArgs(mockResult.Object));

        // Act
        Task resultTask = sut.ListenForAttention(CancellationToken.None);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttention_SecondEventDoesNothing()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        Task resultTask = sut.ListenForAttention(CancellationToken.None);

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
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAttention_StopsListeningOnError()
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
        Task resultTask = sut.ListenForAttention(CancellationToken.None);

        // Assert
        TaskAssert.IsFaulted(resultTask, expectedException, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_ErrorInListeningMethod, "ListenForAttention", expectedException),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNo_WaitsForYesOrNo()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        // Act
        Task<bool> resultTask = sut.ListenForYesNo(CancellationToken.None);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNo_StopsWaitingWhenCancelled()
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
        Task<bool> resultTask = sut.ListenForYesNo(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_CancelledListeningMethod, "ListenForYesNo"),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNo_ThrowsIfAlreadyWaitingForYesNo()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        _ = sut.ListenForYesNo(CancellationToken.None);

        // Act
        Task<bool> resultTask = sut.ListenForYesNo(CancellationToken.None);

        // Assert
        TaskAssert.IsFaulted(resultTask, new InvalidOperationException("ListenForYesNo is already in progress"), nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            "ListenForYesNo is already in progress");
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNo_CompletesAndStopsListeningWhenYesDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        Task<bool> resultTask = sut.ListenForYesNo(CancellationToken.None);

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
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNo_CompletesAndStopsListeningWhenNoDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        Task<bool> resultTask = sut.ListenForYesNo(CancellationToken.None);

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
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNo_DoesNotCompleteWhenYesOrNoNotDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        Task<bool> resultTask = sut.ListenForYesNo(CancellationToken.None);

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
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNo_CanCallAgainAfterComplete()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Exactly(2));
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        _ = sut.ListenForYesNo(CancellationToken.None);

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("YES");
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognitionResultEventArgs(mockResult.Object));

        // Act
        Task<bool> resultTask = sut.ListenForYesNo(CancellationToken.None);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNo_SecondAttemptDoesNothing()
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

        Task<bool> resultTask = sut.ListenForYesNo(CancellationToken.None);
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognitionResultEventArgs(mockResult.Object));

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognitionResultEventArgs(mockResult.Object));

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForYesNo_StopsListeningOnError()
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
        Task<bool> resultTask = sut.ListenForYesNo(CancellationToken.None);

        // Assert
        TaskAssert.IsFaulted(resultTask, expectedException, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_ErrorInListeningMethod, "ListenForYesNo", expectedException),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommands_MoveNextAsync_WaitsForCommand()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommands(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        // Act
        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommands_StopsWaitingWhenCancelled()
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

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommands(cts.Token);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsCanceled(resultTask, 1000, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_CancelledListeningMethod, "ListenForCommands"),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommands_ThrowsIfAlreadyWaitingForCommands()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        _ = sut.ListenForCommands(CancellationToken.None);

        try
        {
            // Act
            _ = sut.ListenForCommands(CancellationToken.None);

            // Assert
            Assert.Fail("Expected exception was not thrown");
        }
        catch (InvalidOperationException result)
        {
            Assert.AreEqual("ListenForCommands is already in progress", result.Message, nameof(result) + ".Message");
        }

        // Assert
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            "ListenForCommands is already in progress");
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommands_MoveNextAsync_CompletesWhenCommandDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommands(CancellationToken.None);
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
        TaskAssert.ResultEquals(resultTask, true, nameof(resultTask));
        Assert.AreSame(mockResult.Object, resultIter.Current, nameof(resultIter) + ".Current");
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommands_DoesNotCompleteWhenNonCommandDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommands(CancellationToken.None);
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
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommands_MoveNextAsync_CompletesWhenStopListeningDetected()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommands(CancellationToken.None);
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
        TaskAssert.ResultEquals(resultTask, false, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommands_CanCallAgainAfterComplete()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Exactly(2));
        MockEngine
            .Setup(x => x.RecognizeAsyncCancel())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommands(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        ValueTask<bool> firstTask = resultIter.MoveNextAsync();

        Mock<IRecognitionResult> mockResult = new();
        mockResult
            .SetupGet(x => x.SemanticMeaning)
            .Returns("STOPLISTENING");

        RecognitionResultEventArgs eventArgs = new RecognitionResultEventArgs(mockResult.Object);
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);
        TaskAssert.ResultEquals(firstTask, false, nameof(firstTask));

        // Act
        ValueTask<bool> resultTask = sut.ListenForCommands(CancellationToken.None).GetAsyncEnumerator().MoveNextAsync();

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommands_SecondEventIsAlsoReturned()
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

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommands(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        ValueTask<bool> firstTask = resultIter.MoveNextAsync();

        RecognitionResultEventArgs eventArgs = new RecognitionResultEventArgs(mockResult.Object);
        MockEngine.Raise(x => x.SpeechRecognized -= null, eventArgs);
        TaskAssert.ResultEquals(firstTask, true, nameof(firstTask));

        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, new RecognitionResultEventArgs(mockResult.Object));

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForCommands_StopsListeningOnError()
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

        IAsyncEnumerable<IRecognitionResult> resultEnum = sut.ListenForCommands(CancellationToken.None);
        Assert.IsNotNull(resultEnum, nameof(resultEnum));

        IAsyncEnumerator<IRecognitionResult> resultIter = resultEnum.GetAsyncEnumerator();

        // Act
        ValueTask<bool> resultTask = resultIter.MoveNextAsync();

        // Assert
        TaskAssert.IsFaulted(resultTask, expectedException, nameof(resultTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_StartsListeningOnlyOnce()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandEnum = sut.ListenForCommands(CancellationToken.None);

        // Act
        Task attentionTask = sut.ListenForAttention(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNo(CancellationToken.None);
        ValueTask<bool> commandTask = commandEnum.GetAsyncEnumerator().MoveNextAsync();

        // Assert
        TaskAssert.IsNotComplete(attentionTask, nameof(attentionTask));
        TaskAssert.IsNotComplete(yesNoTask, nameof(yesNoTask));
        TaskAssert.IsNotComplete(commandTask, nameof(commandTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesListenForAttention()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandEnum = sut.ListenForCommands(CancellationToken.None);

        Task attentionTask = sut.ListenForAttention(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNo(CancellationToken.None);
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
        TaskAssert.IsNotComplete(commandTask, nameof(commandTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesListenForYesNo()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandEnum = sut.ListenForCommands(CancellationToken.None);

        Task attentionTask = sut.ListenForAttention(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNo(CancellationToken.None);
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
        TaskAssert.IsNotComplete(commandTask, nameof(commandTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, false));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesListenForCommands_MoveNextAsync()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandEnum = sut.ListenForCommands(CancellationToken.None);

        Task attentionTask = sut.ListenForAttention(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNo(CancellationToken.None);
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
        TaskAssert.ResultEquals(commandTask, true, nameof(commandTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true));
    }

    [TestMethod]
    public void SpeechRecognition_ListenForAll_CompletesListenForCommands()
    {
        // Arrange
        ISpeechRecognition sut = CreateSut();

        MockEngine
            .Setup(x => x.RecognizeAsync())
            .Verifiable(Times.Once);

        IAsyncEnumerable<IRecognitionResult> commandEnum = sut.ListenForCommands(CancellationToken.None);

        Task attentionTask = sut.ListenForAttention(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNo(CancellationToken.None);
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
        TaskAssert.ResultEquals(commandTask, false, nameof(commandTask));
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, false));
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

        IAsyncEnumerable<IRecognitionResult> commandEnum = sut.ListenForCommands(CancellationToken.None);

        Task attentionTask = sut.ListenForAttention(CancellationToken.None);
        Task<bool> yesNoTask = sut.ListenForYesNo(CancellationToken.None);
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
        TaskAssert.ResultEquals(commandTask, false, nameof(commandTask));
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled");
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled");
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled");

        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_Listening, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, true),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockCommandsGrammar.Name, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockAttentionGrammar.Name, false),
            string.Format(LoggingMessages.SpeechRecognition_Listening, false),
            string.Format(LoggingMessages.SpeechRecognition_GrammarEnabled, MockYesNoGrammar.Name, false));
    }

    // TODO: Handling multiple listening calls at once (e.g. commands and Yes/No)
    //   Start/stop listening correctly
    //   Start/stop grammars correctly
    //   Results are routed to the correct command
}
