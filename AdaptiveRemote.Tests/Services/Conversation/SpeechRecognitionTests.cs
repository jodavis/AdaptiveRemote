using System.Speech.Recognition;
using AdaptiveRemote.TestUtilities;
using Moq;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class SpeechRecognitionTests
{
    private static readonly TimeSpan ResultTimeout = TimeSpan.FromMilliseconds(100);

    private readonly MockLogger<SpeechRecognition> MockLogger = new();
    private readonly Mock<ISpeechRecognitionEngine> MockEngine = new();
    private readonly Mock<IGrammarProvider> MockGrammarProvider = new();
    private readonly Mock<IListeningController> MockListening = new();
    private readonly Mock<IDisposable> MockListenDisposable = new();
    private readonly MockOptions<ConversationSettings> MockOptions = new();

    private readonly Grammar MockAttentionGrammar = new(new GrammarBuilder("Attention")) { Name = nameof(MockAttentionGrammar) };
    private readonly Grammar MockCommandsGrammar = new(new GrammarBuilder("Commands")) { Name = nameof(MockCommandsGrammar) };
    private readonly Grammar MockYesNoGrammar = new(new GrammarBuilder("YesNo")) { Name = nameof(MockYesNoGrammar) };
    private readonly Grammar MockCorrectionGrammar = new(new GrammarBuilder("Correction")) { Name = nameof(MockCorrectionGrammar) };

    public TestContext? TestContext { get; set; }

    private ISpeechRecognition CreateSut() => new SpeechRecognition(MockOptions, MockEngine.Object, MockListening.Object, MockGrammarProvider.Object, MockLogger);

    private static IRecognizedSpeech CreateMockSpeech(params string[] semanticValues)
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
            .Setup(x => x.LoadGrammar(PhraseKinds.WakeWord))
            .Returns(MockAttentionGrammar)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.LoadGrammar(MockAttentionGrammar))
            .Callback(() => Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar) + ".Enabled"))
            .Verifiable(Times.Once);

        MockCommandsGrammar.Enabled = true;
        MockGrammarProvider
            .Setup(x => x.LoadGrammar(PhraseKinds.Commands))
            .Returns(MockCommandsGrammar)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.LoadGrammar(MockCommandsGrammar))
            .Callback(() => Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar) + ".Enabled"))
            .Verifiable(Times.Once);

        MockYesNoGrammar.Enabled = true;
        MockGrammarProvider
            .Setup(x => x.LoadGrammar(PhraseKinds.Confirmation))
            .Returns(MockYesNoGrammar)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.LoadGrammar(MockYesNoGrammar))
            .Callback(() => Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar) + ".Enabled"))
            .Verifiable(Times.Once);

        MockCorrectionGrammar.Enabled = true;
        MockGrammarProvider
            .Setup(x => x.LoadGrammar(PhraseKinds.Correction))
            .Returns(MockCorrectionGrammar)
            .Verifiable(Times.Once);
        MockEngine
            .Setup(x => x.LoadGrammar(MockCorrectionGrammar))
            .Callback(() => Assert.IsFalse(MockCorrectionGrammar.Enabled, nameof(MockCorrectionGrammar) + ".Enabled"))
            .Verifiable(Times.Once);

        MockEngine
            .Setup(x => x.UnloadAllGrammars())
            .Callback(() => MockEngine.Verify(x => x.LoadGrammar(It.IsAny<Grammar>()), Times.Never))
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

    [TestMethod]
    public void SpeechRecognition_Ctor_LoadsGrammars()
    {
        // Arrange

        // Act
        _ = CreateSut();

        // Assert
    }

    [TestMethod]
    public void SpeechRecognition_SetFilter_All_EnablesAllGrammars()
    {
        // Arrange
        PhraseKinds input = PhraseKinds.All;

        MockOptions.Value.ListeningCPU = 87;
        MockOptions.Value.ListeningConfidenceThreshold = 12;

        Expect_UpdateRecognizerSetting_CPUUsage(MockOptions.Value.ListeningCPU);
        Expect_UpdateRecognizerSetting_ConfidenceThreshold(MockOptions.Value.ListeningConfidenceThreshold);

        ISpeechRecognition sut = CreateSut();

        // Act
        sut.SetFilter(input);

        // Assert
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar));
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar));
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar));
        Assert.IsTrue(MockCorrectionGrammar.Enabled, nameof(MockCorrectionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_SetFilter_None_DisablesAllGrammars()
    {
        // Arrange
        PhraseKinds input = PhraseKinds.None;

        ISpeechRecognition sut = CreateSut();
        sut.SetFilter(PhraseKinds.All);

        Expect_UpdateRecognizerSetting_IsNotCalled();

        // Act
        sut.SetFilter(input);

        // Assert
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar));
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar));
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar));
        Assert.IsFalse(MockCorrectionGrammar.Enabled, nameof(MockCorrectionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_SetFilter_Some_EnablesSomeGrammars()
    {
        // Arrange
        PhraseKinds input = PhraseKinds.Commands | PhraseKinds.Confirmation;

        MockOptions.Value.ListeningCPU = 87;
        MockOptions.Value.ListeningConfidenceThreshold = 12;

        Expect_UpdateRecognizerSetting_CPUUsage(MockOptions.Value.ListeningCPU);
        Expect_UpdateRecognizerSetting_ConfidenceThreshold(MockOptions.Value.ListeningConfidenceThreshold);

        ISpeechRecognition sut = CreateSut();

        // Act
        sut.SetFilter(input);

        // Assert
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar));
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar));
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar));
        Assert.IsFalse(MockCorrectionGrammar.Enabled, nameof(MockCorrectionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_SetFilter_Some_DisabledsSomeGrammars()
    {
        // Arrange
        PhraseKinds input = PhraseKinds.Commands | PhraseKinds.Confirmation;

        MockOptions.Value.ListeningCPU = 87;
        MockOptions.Value.ListeningConfidenceThreshold = 12;

        ISpeechRecognition sut = CreateSut();
        sut.SetFilter(PhraseKinds.All);

        Expect_UpdateRecognizerSetting_CPUUsage(MockOptions.Value.ListeningCPU);
        Expect_UpdateRecognizerSetting_ConfidenceThreshold(MockOptions.Value.ListeningConfidenceThreshold);

        // Act
        sut.SetFilter(input);

        // Assert
        Assert.IsFalse(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar));
        Assert.IsTrue(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar));
        Assert.IsTrue(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar));
        Assert.IsFalse(MockCorrectionGrammar.Enabled, nameof(MockCorrectionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_SetFilter_WakeWord_SetsWakeWordSettings()
    {
        // Arrange
        PhraseKinds input = PhraseKinds.WakeWord;

        MockOptions.Value.WakeWordCPU = 26;
        MockOptions.Value.WakeWordConfidenceThreshold = 90;

        Expect_UpdateRecognizerSetting_CPUUsage(MockOptions.Value.WakeWordCPU);
        Expect_UpdateRecognizerSetting_ConfidenceThreshold(MockOptions.Value.WakeWordConfidenceThreshold);

        ISpeechRecognition sut = CreateSut();

        // Act
        sut.SetFilter(input);

        // Assert
        Assert.IsTrue(MockAttentionGrammar.Enabled, nameof(MockAttentionGrammar));
        Assert.IsFalse(MockCommandsGrammar.Enabled, nameof(MockCommandsGrammar));
        Assert.IsFalse(MockYesNoGrammar.Enabled, nameof(MockYesNoGrammar));
        Assert.IsFalse(MockCorrectionGrammar.Enabled, nameof(MockCorrectionGrammar));
    }

    [TestMethod]
    public void SpeechRecognition_RecognizeAsync_StartsListeningAndWaitsForRecognizedEvent()
    {
        // Arrange
        Expect_ListenAsync();

        ISpeechRecognition sut = CreateSut();

        IAsyncEnumerable<IRecognizedSpeech> enumerable = sut.RecognizeAsync(default);
        IAsyncEnumerator<IRecognizedSpeech> enumerator = enumerable.GetAsyncEnumerator();

        // Act
        ValueTask<bool> resultTask = enumerator.MoveNextAsync();

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));
    }

    [TestMethod]
    public void SpeechRecognition_RecognizeAsync_ReturnsRecognizedSpeechOnRecognizedEvent()
    {
        // Arrange
        Expect_ListenAsync();

        ISpeechRecognition sut = CreateSut();

        IRecognizedSpeech expectedSpeech = CreateMockSpeech();
        RecognizedSpeechEventArgs expectedEventArgs = new(expectedSpeech);

        IAsyncEnumerable<IRecognizedSpeech> enumerable = sut.RecognizeAsync(default);
        IAsyncEnumerator<IRecognizedSpeech> enumerator = enumerable.GetAsyncEnumerator();

        ValueTask<bool> resultTask = enumerator.MoveNextAsync();

        // Act
        MockEngine.Raise(x => x.SpeechRecognized -= null, expectedEventArgs);

        // Assert
        TaskAssert.ResultEquals(resultTask, true, TimeSpan.FromSeconds(1), nameof(resultTask));
        Assert.AreSame(expectedSpeech, enumerator.Current, nameof(enumerator) + ".Current");
    }

    [TestMethod]
    public void SpeechRecognition_RecognizeAsync_DetachesEventAndStopsListeningOnStopToken()
    {
        // Arrange
        Expect_ListenAsync();
        Expect_ListenDisposed();
        Expect_SpeechRecognized_EventHandlerRemoved();

        CancellationTokenSource cts = new();

        ISpeechRecognition sut = CreateSut();

        IAsyncEnumerable<IRecognizedSpeech> enumerable = sut.RecognizeAsync(cts.Token);
        IAsyncEnumerator<IRecognizedSpeech> enumerator = enumerable.GetAsyncEnumerator();

        ValueTask<bool> resultTask = enumerator.MoveNextAsync();

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsCanceled(resultTask, TimeSpan.FromSeconds(1), nameof(resultTask));
    }

    [TestMethod]
    public void SpeechRecognition_RecognizeAsync_DoesNotStartIfStopTokenAlreadyCancelled()
    {
        // Arrange
        Expect_SpeechRecognized_EventHandlerNotAdded();

        CancellationTokenSource cts = new();

        ISpeechRecognition sut = CreateSut();

        cts.Cancel();

        IAsyncEnumerable<IRecognizedSpeech> enumerable = sut.RecognizeAsync(cts.Token);
        IAsyncEnumerator<IRecognizedSpeech> enumerator = enumerable.GetAsyncEnumerator();

        // Act
        ValueTask<bool> resultTask = enumerator.MoveNextAsync();

        // Assert
        TaskAssert.IsCanceled(resultTask, TimeSpan.FromSeconds(1), nameof(resultTask));
    }

    private void Expect_UpdateRecognizerSetting_CPUUsage(int value)
        => MockEngine
            .Setup(x => x.UpdateRecognizerSetting(RecognizerConstants.CPUUsage, It.IsAny<int>()))
            .WithArgumentValidation(nameof(value), value)
            .Verifiable(Times.Once);
    private void Expect_UpdateRecognizerSetting_ConfidenceThreshold(int value)
        => MockEngine
            .Setup(x => x.UpdateRecognizerSetting(RecognizerConstants.ConfidenceThreshold, It.IsAny<int>()))
            .WithArgumentValidation(nameof(value), value)
            .Verifiable(Times.Once);
    private void Expect_UpdateRecognizerSetting_IsNotCalled()
        => MockEngine
            .Setup(x => x.UpdateRecognizerSetting(It.IsAny<string>(), It.IsAny<int>()))
            .Verifiable(Times.Never);

    private void Expect_ListenAsync()
        => MockListening
            .Setup(x => x.Listen())
            .Returns(MockListenDisposable.Object)
            .Verifiable(Times.Once);
    private void Expect_ListenDisposed()
        => MockListenDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);
    private void Expect_SpeechRecognized_EventHandlerRemoved()
        => MockEngine
            .SetupAdd(x => x.SpeechRecognized += It.IsAny<EventHandler<RecognizedSpeechEventArgs>>())
            .Callback(delegate (EventHandler<RecognizedSpeechEventArgs> addedHandler)
            {
                MockEngine
                    .SetupRemove(x => x.SpeechRecognized -= addedHandler)
                    .Verifiable(Times.Once);
            })
            .Verifiable(Times.Once);
    private void Expect_SpeechRecognized_EventHandlerNotAdded()
        => MockEngine
            .SetupAdd(x => x.SpeechRecognized += It.IsAny<EventHandler<RecognizedSpeechEventArgs>>())
            .Verifiable(Times.Never);
}
