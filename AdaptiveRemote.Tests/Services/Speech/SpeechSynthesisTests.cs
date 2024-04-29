using Microsoft.Extensions.Options;
using Moq;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class SpeechSynthesisTests
{
    private readonly MockLogger<SpeechSynthesis> MockLogger = new();
    private readonly Mock<ISpeechSynthesizer> MockSynthesizer = new();
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
            .Setup(x => x.SetOutputToDefaultAudioDevice())
            .Verifiable(Times.Once);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockSynthesizer.Verify();
    }

    private ISpeechSynthesis CreateSut()
    {
        Mock<IOptionsSnapshot<ConversationSettings>> mockOptionsSnapshot = new();
        mockOptionsSnapshot
            .SetupGet(x => x.Value)
            .Returns(SpeechSettings);

        return new SpeechSynthesis(
            MockSynthesizer.Object,
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
            string.Format(LoggingMessages.SpeechSynthesis_SelectedVoice, InstalledVoices[1]));
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
            string.Format(LoggingMessages.SpeechSynthesis_VoiceNotFound, "Missile"));
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
            string.Format(LoggingMessages.SpeechSynthesis_VoiceNotFound, "Missile"),
            string.Format(LoggingMessages.SpeechSynthesis_SelectedVoice, InstalledVoices[1]));
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
            string.Format(LoggingMessages.SpeechSynthesis_SelectedVoice, InstalledVoices[1]));
    }

    [TestMethod]
    public void SpeechSynthesis_Say_LogsPhraseAndSendsToSynthesizer()
    {
        // Arrange
        const string input = "Hello World!";

        MockSynthesizer
            .Setup(x => x.SpeakAsync(input))
            .Verifiable(Times.Once);
        MockSynthesizer
            .Setup(x => x.CancelAll())
            .Callback(() => MockSynthesizer.Verify(x => x.SpeakAsync(It.IsAny<string>()), Times.Never))
            .Verifiable();

        ISpeechSynthesis sut = CreateSut();

        // Act
        sut.Say(input);

        // Assert
        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechSynthesis_VoiceNotFound, SpeechSettings.Voice[0]),
            string.Format(LoggingMessages.SpeechSynthesis_SelectedVoice, InstalledVoices[0]),
            string.Format(LoggingMessages.SpeechSynthesis_Saying, input));
    }
}
