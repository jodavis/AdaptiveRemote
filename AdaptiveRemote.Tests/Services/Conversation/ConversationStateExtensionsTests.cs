using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using AdaptiveRemote.TestUtilities;
using Moq;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class ConversationStateExtensionsTests
{
    private readonly MockLogger<ConversationState> MockLogger = new();

    private static readonly IReadOnlyDictionary<string, Command> MockCommands = new Dictionary<string, Command>
    {
        ["Play"] = new TiVoCommand("Play", speakPhrase: "Playing...")
    };

    private static IRecognizedSpeech CreateMockSpeech(string text)
        => CreateMockSpeech(text, new());
    private static IRecognizedSpeech CreateMockSpeech(string text, Dictionary<string, string> semantics)
    {
        Mock<IRecognizedSpeech> mockSpeech = new();
        mockSpeech
            .SetupGet(x => x.Text)
            .Returns(text);

        string? value = null;
        mockSpeech
            .Setup(x => x.TryGetSemanticValue(It.IsAny<string>(), out value))
            .Returns(false);
        mockSpeech
            .Setup(x => x.ContainsSemanticValue(It.IsAny<string>()))
            .Returns(false);
        mockSpeech
            .Setup(x => x.ToString())
            .Returns($"{text} {{ {string.Join(", ", semantics.Select(x => $"{x.Key}:{x.Value}"))} }}");
        foreach (KeyValuePair<string, string> pair in semantics)
        {
            value = pair.Value;
            mockSpeech
                .Setup(x => x.TryGetSemanticValue(pair.Key, out value))
                .Returns(true);
            mockSpeech
                .Setup(x => x.ContainsSemanticValue(pair.Key))
                .Returns(true);
        }

        return mockSpeech.Object;
    }

    // Logging Messages
    private static string ExpectMessage_Updated(ConversationState state)
        => $"Information[1301]: {LoggingMessages.ConverationState_Updated.AsMessageTemplate(state)}";
    private static string ExpectMessage_Recognized(string text, string semantics)
        => $"Information[207]: {string.Format(LoggingMessages.ConversationController_Recognized, text, semantics)}";
    private static string Expected_Executing(string command)
        => $"Information[210]: {string.Format(LoggingMessages.ConversationController_Executing, command)}";
    private static string Expected_Executed(string command)
        => $"Information[211]: {string.Format(LoggingMessages.ConversationController_Executed, command)}";
    private static string Expected_UnknownCommand(string command)
        => $"Error[208]: {string.Format(LoggingMessages.ConversationController_UnknownCommand, command)}";
    private static string Expected_CommandMissingExecuteAction(string command)
        => $"Error[213]: {string.Format(LoggingMessages.ConversationController_CommandMissingExecuteAction, command)}";
    private static string Expected_CommandDisabled(string command)
        => $"Error[214]: {string.Format(LoggingMessages.ConversationController_CommandDisabled, command)}";

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_WakeWords_EntersListeningMode()
    {
        // Arrange
        IRecognizedSpeech lastCommand = CreateMockSpeech("Play");

        IRecognizedSpeech input = CreateMockSpeech("Hey Remote", new()
        {
            ["system"] = "STARTLISTENING"
        });

        ConversationState sut = new(MockCommands, WantsPhrases: PhraseKinds.WakeWord, LastCommand: lastCommand);

        ConversationState expected = sut with
        {
            WantsPhrases = PhraseKinds.Commands,
            LastCommand = null,
            LastResponse = new([Phrases.Conversation_ImListening], [])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_Updated(expected));
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_StopListening_LeavesListeningMode()
    {
        // Arrange
        IRecognizedSpeech lastCommand = CreateMockSpeech("Play");

        IRecognizedSpeech input = CreateMockSpeech("Stop listening", new()
        {
            ["system"] = "STOPLISTENING"
        });

        ConversationState sut = new(MockCommands, WantsPhrases: PhraseKinds.Commands, LastCommand: lastCommand);

        ConversationState expected = sut with
        {
            WantsPhrases = PhraseKinds.WakeWord,
            LastCommand = null,
            LastResponse = new([Phrases.Conversation_StoppedListening], [])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_Updated(expected));
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_ThankYou_LeavesListeningModeAndSaysYoureWelcom()
    {
        // Arrange
        IRecognizedSpeech lastCommand = CreateMockSpeech("Play");

        IRecognizedSpeech input = CreateMockSpeech("Thank you", new()
        {
            ["system"] = "STOPLISTENING",
            ["thankyou"] = "true"
        });

        ConversationState sut = new(MockCommands, WantsPhrases: PhraseKinds.Commands, LastCommand: lastCommand);

        ConversationState expected = sut with
        {
            WantsPhrases = PhraseKinds.WakeWord,
            LastCommand = null,
            LastResponse = new([Phrases.Conversation_YoureWelcome], [])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_Updated(expected));
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_SimpleCommand_ReturnsCommandAndUpdatesState()
    {
        // Arrange
        IRecognizedSpeech input = CreateMockSpeech("Play", new()
        {
            ["command"] = "Play"
        });

        ConversationState sut = new(MockCommands, WantsPhrases: PhraseKinds.Commands);

        ConversationState expected = sut with
        {
            LastCommand = input,
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Correction,
            LastResponse = new([MockCommands["Play"].SpeakPhrase], [MockCommands["Play"]])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_Recognized(input.Text, "Play"),
            ExpectMessage_Updated(expected));
    }
}
