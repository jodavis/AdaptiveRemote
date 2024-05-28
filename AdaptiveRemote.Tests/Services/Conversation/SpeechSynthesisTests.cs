using AdaptiveRemote.Logging;
using AdaptiveRemote.TestUtilities;
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
            .Setup(x => x.SpeakAsync(It.IsAny<string>()))
            .Verifiable(Times.Never);
        MockSynthesizer
            .Setup(x => x.GetInstalledVoices())
            .Returns(InstalledVoices);
        MockSynthesizer
            .Setup(x => x.CancelAll())
            .Verifiable(Times.Never);

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

    private static string Expected_SelectedVoice(string voiceName)
        => $"Information[401]: {string.Format(LoggingMessages.SpeechSynthesis_SelectedVoice, voiceName)}";
    private static string Expected_VoiceNotFound(string voiceName)
        => $"Warning[402]: {string.Format(LoggingMessages.SpeechSynthesis_VoiceNotFound, voiceName)}";
    private static string Expected_Saying(string phrase)
        => $"Information[403]: {string.Format(LoggingMessages.SpeechSynthesis_Saying, phrase)}";
    private static string Expected_CancelledSaying(string phrase)
        => $"Information[404]: {string.Format(LoggingMessages.SpeechSynthesis_CancelledSaying, phrase)}";

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
        MockLogger.VerifyMessages(
            Expected_SelectedVoice(InstalledVoices[1]));
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
        MockLogger.VerifyMessages(
            Expected_VoiceNotFound("Missile"));
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
        MockLogger.VerifyMessages(
            Expected_VoiceNotFound("Missile"),
            Expected_SelectedVoice(InstalledVoices[1]));
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
        MockLogger.VerifyMessages(
            Expected_SelectedVoice(InstalledVoices[1]));
    }

    [TestMethod]
    public void SpeechSynthesis_SayAsync_LogsPhraseAndSendsToSynthesizer()
    {
        // Arrange
        const string input = "Hello World!";

        MockSynthesizer
            .Setup(x => x.SpeakAsync(input))
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
        TaskAssert.IsComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_VoiceNotFound(SpeechSettings.Voice[0]),
            Expected_SelectedVoice(InstalledVoices[0]),
            Expected_Saying(input));
    }

    [TestMethod]
    public void SpeechSynthesis_SayAsync_WaitsForSpeakCompleted()
    {
        // Arrange
        const string input = "Hello World!";

        MockSynthesizer
            .Setup(x => x.SpeakAsync(input))
            .Verifiable(Times.Once);

        MockController
            .Setup(x => x.Pause())
            .Returns(MockPauseDisposable.Object)
            .Verifiable(Times.Once);

        ISpeechSynthesis sut = CreateSut();

        // Act
        Task resultTask = sut.SayAsync(input, default);

        // Assert
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_VoiceNotFound(SpeechSettings.Voice[0]),
            Expected_SelectedVoice(InstalledVoices[0]),
            Expected_Saying(input));
    }

    [TestMethod]
    public void SpeechSynthesis_SayAsync_WaitsForSpeakCompletedAfterPreviousSpeakCompleted()
    {
        // Arrange
        const string input = "Hello World!";

        MockSynthesizer
            .Setup(x => x.SpeakAsync(input))
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
        TaskAssert.IsNotComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_VoiceNotFound(SpeechSettings.Voice[0]),
            Expected_SelectedVoice(InstalledVoices[0]),
            Expected_Saying(input),
            Expected_Saying(input));
    }

    [TestMethod]
    public void SpeechSynthesis_SayAsync_WhenCancelled_CallsCancelAll()
    {
        // Arrange
        const string input = "Hello World!";

        MockSynthesizer
            .Setup(x => x.SpeakAsync(input))
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
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_VoiceNotFound(SpeechSettings.Voice[0]),
            Expected_SelectedVoice(InstalledVoices[0]),
            Expected_Saying(input),
            Expected_CancelledSaying(input));
    }

    [TestMethod]
    public void SpeechSynthesis_SayAsync_WhenCancelled_CompleteEventDoesNothing()
    {
        // Arrange
        const string input = "Hello World!";

        MockSynthesizer
            .Setup(x => x.SpeakAsync(input))
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
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_VoiceNotFound(SpeechSettings.Voice[0]),
            Expected_SelectedVoice(InstalledVoices[0]),
            Expected_Saying(input),
            Expected_CancelledSaying(input));
    }

    [TestMethod]
    public void SpeechSynthesis_SayAsync_WhenCompleted_CancellationDoesNothing()
    {
        // Arrange
        const string input = "Hello World!";

        MockSynthesizer
            .Setup(x => x.SpeakAsync(input))
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
        TaskAssert.IsComplete(resultTask, nameof(resultTask));

        MockLogger.VerifyMessages(
            Expected_VoiceNotFound(SpeechSettings.Voice[0]),
            Expected_SelectedVoice(InstalledVoices[0]),
            Expected_Saying(input));
    }
}
