using Microsoft.Extensions.Options;
using Moq;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class SpeechSynthesisTests
{
    private readonly MockLogger<SpeechSynthesis> MockLogger = new();
    private readonly Mock<ISpeechSynthesizer> MockSynthesizer = new();
    private readonly Mock<IListeningController> MockController = new();
    private readonly Mock<IDisposable> MockPauseDisposable = new();
    private readonly ConversationSettings SpeechSettings = new();

    private readonly string[] InstalledVoices = [
        "Microsoft Zira - English",
        "Microsoft Quimby - English"
    ];

    public SpeechSynthesisTests()
    {
        MockSynthesizer
            .Setup(x => x.Speak(It.IsAny<string>()))
            .Verifiable(Times.Never);
        MockSynthesizer
            .Setup(x => x.GetInstalledVoices())
            .Returns(InstalledVoices);
        MockSynthesizer
            .Setup(x => x.CancelAll())
            .Verifiable(Times.Never);
        MockSynthesizer
            .Setup(x => x.SetSpeakingRate(It.IsAny<int>()))
            .Verifiable(Times.Once);

        MockController
            .Setup(x => x.Listen())
            .Verifiable(Times.Never);
        MockController
            .Setup(x => x.Pause())
            .Verifiable(Times.Never);

        MockPauseDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Never);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockSynthesizer.Verify();
        MockController.Verify();
        MockPauseDisposable.Verify();
    }

    private ISpeechSynthesis CreateSut()
    {
        Mock<IOptionsSnapshot<ConversationSettings>> mockOptionsSnapshot = new();
        mockOptionsSnapshot
            .SetupGet(x => x.Value)
            .Returns(SpeechSettings);

        return new SpeechSynthesis(
            MockSynthesizer.Object,
            MockController.Object,
            mockOptionsSnapshot.Object,
            MockLogger);
    }

    [TestMethod]
    public void SpeechSynthesis_Constructor_SelectsVoiceFromSettings()
    {
        // Arrange
        SpeechSettings.Voice = ["Quimby"];

        MockSynthesizer
            .Setup(x => x.SelectVoice(InstalledVoices[1]))
            .Verifiable(Times.Once);

        // Act
        ISpeechSynthesis sut = CreateSut();

        // Assert
        MockLogger.VerifyMessages(log => log.SpeechSynthesis_SelectedVoice(InstalledVoices[1]));
    }

    [TestMethod]
    public void SpeechSynthesis_Constructor_WithInvalidVoiceName_LogsWarning()
    {
        // Arrange
        SpeechSettings.Voice = ["Missile"];

        MockSynthesizer
            .Setup(x => x.SelectVoice("Missile"))
            .Verifiable(Times.Never);

        // Act
        ISpeechSynthesis sut = CreateSut();

        // Assert
        MockLogger.VerifyMessages(log => log.SpeechSynthesis_VoiceNotFound("Missile"));
    }

    [TestMethod]
    public void SpeechSynthesis_Constructor_WithFallbackVoiceName_LogsWarningAndUsesFallback()
    {
        // Arrange
        SpeechSettings.Voice = ["Missile", "Quimby"];

        MockSynthesizer
            .Setup(x => x.SelectVoice(It.IsAny<string>()))
            .Verifiable(Times.Never);
        MockSynthesizer
            .Setup(x => x.SelectVoice(InstalledVoices[1]))
            .Verifiable(Times.Once);

        // Act
        ISpeechSynthesis sut = CreateSut();

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.SpeechSynthesis_VoiceNotFound("Missile");
            log.SpeechSynthesis_SelectedVoice(InstalledVoices[1]);
        });
    }

    [TestMethod]
    public void SpeechSynthesis_Constructor_WithFallbackVoiceName_DoesNotTryFallbackIfFirstVoiceIsFound()
    {
        // Arrange
        SpeechSettings.Voice = ["Quimby", "Missile"];

        MockSynthesizer
            .Setup(x => x.SelectVoice(It.IsAny<string>()))
            .Verifiable(Times.Never);
        MockSynthesizer
            .Setup(x => x.SelectVoice(InstalledVoices[1]))
            .Verifiable(Times.Once);

        // Act
        ISpeechSynthesis sut = CreateSut();

        // Assert
        MockLogger.VerifyMessages(log => log.SpeechSynthesis_SelectedVoice(InstalledVoices[1]));
    }

    [TestMethod]
    public void SpeechSynthesis_Constructor_WithCustomizedSpeakingRate_CallsSetSpeakingRate()
    {
        // Arrange
        SpeechSettings.SpeakingRate = 75;

        MockSynthesizer
            .Setup(x => x.SetSpeakingRate(It.IsAny<int>()))
            .Callback(delegate (int speakingRate) { throw new AssertFailedException($"Wrong speaking rate: {speakingRate}"); });
        MockSynthesizer
            .Setup(x => x.SetSpeakingRate(75))
            .Verifiable(Times.Once);

        // Act
        ISpeechSynthesis sut = CreateSut();

        // Assert
        MockLogger.VerifyMessages(log => log.SpeechSynthesis_SelectedVoice(InstalledVoices[0]));
    }

    [TestMethod]
    public void SpeechSynthesis_SayAsync_LogsPhraseAndSendsToSynthesizer()
    {
        // Arrange
        const string input = "Hello World!";

        MockSynthesizer
            .Setup(x => x.Speak(input))
            .Callback(() => MockController.Verify(x => x.Pause(), Times.Once))
            .Verifiable(Times.Once);

        MockController
            .Setup(x => x.Pause())
            .Returns(MockPauseDisposable.Object)
            .Verifiable(Times.Once);
        MockPauseDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        ISpeechSynthesis sut = CreateSut();

        // Act
        Task resultTask = sut.SayAsync(input, default);
        MockSynthesizer.Raise(x => x.SpeakCompleted -= null, EventArgs.Empty);

        // Assert
        resultTask.Should().BeComplete(because: "SayAsync should complete after SpeakCompleted is raised");

        MockLogger.VerifyMessages(log =>
        {
            log.SpeechSynthesis_SelectedVoice(InstalledVoices[0]);
            log.SpeechSynthesis_Saying(input);
        });
    }

    [TestMethod]
    public void SpeechSynthesis_SayAsync_WaitsForSpeakCompleted()
    {
        // Arrange
        const string input = "Hello World!";

        MockSynthesizer
            .Setup(x => x.Speak(input))
            .Verifiable(Times.Once);

        MockController
            .Setup(x => x.Pause())
            .Returns(MockPauseDisposable.Object)
            .Verifiable(Times.Once);

        ISpeechSynthesis sut = CreateSut();

        // Act
        Task resultTask = sut.SayAsync(input, default);

        // Assert
        resultTask.Should().NotBeComplete(because: "SayAsync is waiting for SpeakCompleted to be raised");

        MockLogger.VerifyMessages(log =>
        {
            log.SpeechSynthesis_SelectedVoice(InstalledVoices[0]);
            log.SpeechSynthesis_Saying(input);
        });
    }

    [TestMethod]
    public void SpeechSynthesis_SayAsync_WaitsForSpeakCompletedAfterPreviousSpeakCompleted()
    {
        // Arrange
        const string input = "Hello World!";

        MockSynthesizer
            .Setup(x => x.Speak(input))
            .Verifiable(Times.Exactly(2));

        MockController
            .Setup(x => x.Pause())
            .Returns(MockPauseDisposable.Object)
            .Verifiable(Times.Exactly(2));
        MockPauseDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        ISpeechSynthesis sut = CreateSut();
        Task firstTask = sut.SayAsync(input, default);
        MockSynthesizer.Raise(x => x.SpeakCompleted -= null, EventArgs.Empty);

        // Act
        Task resultTask = sut.SayAsync(input, default);

        // Assert
        resultTask.Should().NotBeComplete(because: "SayAsync is waiting for SpeakCompleted to be raised");

        MockLogger.VerifyMessages(log =>
        {
            log.SpeechSynthesis_SelectedVoice(InstalledVoices[0]);
            log.SpeechSynthesis_Saying(input);
            log.SpeechSynthesis_Saying(input);
        });
    }

    [TestMethod]
    public void SpeechSynthesis_SayAsync_WhenCancelled_CallsCancelAll()
    {
        // Arrange
        const string input = "Hello World!";

        MockSynthesizer
            .Setup(x => x.Speak(input))
            .Verifiable(Times.Once);
        MockSynthesizer
            .Setup(x => x.CancelAll())
            .Verifiable(Times.Once);

        MockController
            .Setup(x => x.Pause())
            .Returns(MockPauseDisposable.Object)
            .Verifiable(Times.Once);
        MockPauseDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        ISpeechSynthesis sut = CreateSut();

        CancellationTokenSource cts = new();
        Task resultTask = sut.SayAsync(input, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        resultTask.Should().BeCanceled(because: "SayAsync's cancellation token was triggered");

        MockLogger.VerifyMessages(log =>
        {
            log.SpeechSynthesis_SelectedVoice(InstalledVoices[0]);
            log.SpeechSynthesis_Saying(input);
            log.SpeechSynthesis_CancelledSaying(input);
        });
    }

    [TestMethod]
    public void SpeechSynthesis_SayAsync_WhenCancelled_CompleteEventDoesNothing()
    {
        // Arrange
        const string input = "Hello World!";

        MockSynthesizer
            .Setup(x => x.Speak(input))
            .Verifiable(Times.Once);
        MockSynthesizer
            .Setup(x => x.CancelAll())
            .Verifiable(Times.Once);

        MockController
            .Setup(x => x.Pause())
            .Returns(MockPauseDisposable.Object)
            .Verifiable(Times.Once);
        MockPauseDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        ISpeechSynthesis sut = CreateSut();

        CancellationTokenSource cts = new();
        Task resultTask = sut.SayAsync(input, cts.Token);
        cts.Cancel();

        // Act
        MockSynthesizer.Raise(x => x.SpeakCompleted -= null, EventArgs.Empty);

        // Assert
        resultTask.Should().BeCanceled(because: "SayAsync's cancellation token was triggered");

        MockLogger.VerifyMessages(log =>
        {
            log.SpeechSynthesis_SelectedVoice(InstalledVoices[0]);
            log.SpeechSynthesis_Saying(input);
            log.SpeechSynthesis_CancelledSaying(input);
        });
    }

    [TestMethod]
    public void SpeechSynthesis_SayAsync_WhenCompleted_CancellationDoesNothing()
    {
        // Arrange
        const string input = "Hello World!";

        MockSynthesizer
            .Setup(x => x.Speak(input))
            .Verifiable(Times.Once);

        MockController
            .Setup(x => x.Pause())
            .Returns(MockPauseDisposable.Object)
            .Verifiable(Times.Once);
        MockPauseDisposable
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

        ISpeechSynthesis sut = CreateSut();

        CancellationTokenSource cts = new();
        Task resultTask = sut.SayAsync(input, cts.Token);
        MockSynthesizer.Raise(x => x.SpeakCompleted -= null, EventArgs.Empty);

        // Act
        cts.Cancel();

        // Assert
        resultTask.Should().BeComplete(because: "SayAsync should complete after SpeakCompleted is raised");

        MockLogger.VerifyMessages(log =>
        {
            log.SpeechSynthesis_SelectedVoice(InstalledVoices[0]);
            log.SpeechSynthesis_Saying(input);
        });
    }

    [TestMethod]
    public void SpeechSynthesis_SayAsync_ThrowsIfSpeakingIsInProgress()
    {
        // Arrange
        const string input1 = "Hello World!";
        const string input2 = "Hi Planet?!";

        MockSynthesizer
            .Setup(x => x.Speak(input1))
            .Verifiable(Times.Once);
        MockSynthesizer
            .Setup(x => x.Speak(input2))
            .Verifiable(Times.Never);

        MockController
            .Setup(x => x.Pause())
            .Returns(MockPauseDisposable.Object)
            .Verifiable(Times.Once);

        ISpeechSynthesis sut = CreateSut();

        sut.SayAsync(input1, default);

        Exception expectedError = new InvalidOperationException($"Cannot say '{input2}'; another phrase is already in progress");

        // Act
        Task resultTask = sut.SayAsync(input2, default);

        // Assert
        resultTask.Should().BeFaultedWith(expectedError,
            because: "SayAsync should throw if speaking is already in progress");

        MockLogger.VerifyMessages(log =>
        {
            log.SpeechSynthesis_SelectedVoice(InstalledVoices[0]);
            log.SpeechSynthesis_Saying(input1);
            log.SpeechSynthesis_AlreadySpeaking(input2);
        });
    }
}
