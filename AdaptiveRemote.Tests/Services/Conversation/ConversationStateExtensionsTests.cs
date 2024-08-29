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

        ConversationState expectedState = sut with { WantsPhrases = PhraseKinds.Commands, LastCommand = null };

        // Act
        (ConversationState State, ConversationResponse Response) result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expectedState, result.State, nameof(result.State));
        AssertCollectionsEqual([Phrases.Conversation_ImListening], result.Response.Phrases, "result.Response.Phrases");
        AssertCollectionEmpty(result.Response.Commands, "result.Response.Commands");

        MockLogger.VerifyMessages(
            ExpectMessage_Updated(expectedState));
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

        ConversationState expectedState = sut with { WantsPhrases = PhraseKinds.WakeWord, LastCommand = null };

        // Act
        (ConversationState State, ConversationResponse Response) result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expectedState, result.State, nameof(result.State));

        AssertCollectionsEqual([Phrases.Conversation_StoppedListening], result.Response.Phrases, "result.Response.Phrases");
        AssertCollectionEmpty(result.Response.Commands, "result.Response.Commands");

        MockLogger.VerifyMessages(
            ExpectMessage_Updated(expectedState));
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

        ConversationState expectedState = sut with { WantsPhrases = PhraseKinds.WakeWord, LastCommand = null };

        // Act
        (ConversationState State, ConversationResponse Response) result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expectedState, result.State, nameof(result.State));

        AssertCollectionsEqual([Phrases.Conversation_YoureWelcome], result.Response.Phrases, "result.Response.Phrases");
        AssertCollectionEmpty(result.Response.Commands, "result.Response.Commands");

        MockLogger.VerifyMessages(
            ExpectMessage_Updated(expectedState));
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

        ConversationState expectedState = sut with { LastCommand = input, WantsPhrases = PhraseKinds.Commands | PhraseKinds.Correction };

        // Act
        (ConversationState State, ConversationResponse Response) result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expectedState, result.State, nameof(result.State));

        AssertCollectionsEqual([MockCommands["Play"].SpeakPhrase], result.Response.Phrases, "result.Response.Phrases");
        AssertCollectionsEqual([MockCommands["Play"]], result.Response.Commands, "result.Response.Commands");

        MockLogger.VerifyMessages(
            ExpectMessage_Recognized(input.Text, "Play"),
            ExpectMessage_Updated(expectedState));
    }

    private static void AssertCollectionEmpty<ItemType>(IEnumerable<ItemType> actual, string name)
        => AssertCollectionsEqual(Array.Empty<ItemType>(), actual, name);
    private static void AssertCollectionsEqual<ItemType>(IEnumerable<ItemType> expected, IEnumerable<ItemType> actual, string name)
    {
        Assert.IsNotNull(actual, name);

        IEnumerator<ItemType> expectedIter = expected.GetEnumerator();
        IEnumerator<ItemType> actualIter = actual.GetEnumerator();

        int count = 0;

        while (expectedIter.MoveNext())
        {
            if (!actualIter.MoveNext())
            {
                int expectedCount = count;
                List<string> missingMessages = GetRemaining(expectedIter, ref expectedCount);
                Assert.AreEqual(expectedCount, count, "Wrong number of items. Did not find:\n{0}",
                    string.Join("\n", missingMessages));
            }

            Assert.AreEqual($"\n{expectedIter.Current}", $"\n{actualIter.Current}", "{1}[{0}]", count, name);

            count++;
        }

        if (actualIter.MoveNext())
        {
            List<string> unexpectedMessages = GetRemaining(actualIter, ref count);
            Assert.AreEqual(expected.Count(), count,
                "Wrong number of items. Did not expect to find:\n{0}",
                string.Join("\n", unexpectedMessages));
        }

        static List<string> GetRemaining(IEnumerator<ItemType> iter, ref int count)
        {
            List<string> remaining = new();

            do
            {
                remaining.Add($"[{count}]: {iter.Current}");
                count++;
            } while (iter.MoveNext());

            return remaining;
        }
    }
}
