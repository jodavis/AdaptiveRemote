using Microsoft.Extensions.Options;
using Moq;

namespace AdaptiveRemote.Services.Speech;

[TestClass]
public class SpeechSynthesisTests
{
    private readonly MockLogger<SpeechSynthesis> MockLogger = new();
    private readonly Mock<ISpeechSynthesizer> MockSynthesizer = new();
    private readonly SpeechSettings SpeechSettings = new();

    public SpeechSynthesisTests()
    {
        MockSynthesizer
            .Setup(x => x.SpeakAsync(It.IsAny<string>()))
            .Verifiable(Times.Never);
        MockSynthesizer
            .Setup(x => x.HasVoice(It.IsAny<string>()))
            .Returns(true);
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
        Mock<IOptionsSnapshot<SpeechSettings>> mockOptionsSnapshot = new();
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
        SpeechSettings.Voice = "Quimby";

        MockSynthesizer
            .Setup(x => x.SelectVoice("Quimby"))
            .Verifiable(Times.Once);
        MockSynthesizer
            .Setup(x => x.HasVoice("Quimby"))
            .Returns(true)
            .Verifiable(Times.Once);

        // Act
        ISpeechSynthesis sut = CreateSut();

        // Assert
        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechSynthesis_SelectedVoice, "Quimby"));
    }

    [TestMethod]
    public void SpeechSynthesis_Constructor_WithInvalidVoiceName_LogsWarning()
    {
        // Arrange
        SpeechSettings.Voice = "Quimby";

        MockSynthesizer
            .Setup(x => x.SelectVoice("Quimby"))
            .Verifiable(Times.Never);
        MockSynthesizer
            .Setup(x => x.HasVoice("Quimby"))
            .Returns(false)
            .Verifiable(Times.Once);

        // Act
        ISpeechSynthesis sut = CreateSut();

        // Assert
        MockLogger.VerifyMessages(
            string.Format(LoggingMessages.SpeechSynthesis_VoiceNotFound, "Quimby"));
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
            string.Format(LoggingMessages.SpeechSynthesis_SelectedVoice, SpeechSettings.Voice),
            string.Format(LoggingMessages.SpeechSynthesis_Saying, input));
    }
}
