using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using AdaptiveRemote.TestUtilities;
using Moq;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class ConversationStateExtensionsTests
{
    private readonly MockLogger<ConversationState> MockLogger = new();

    private static readonly IReadOnlyDictionary<string, Command> MockCommands = new List<Command>
    {
        new TiVoCommand("Play", speakPhrase: "Playing...") { IsEnabled = true, ExecuteAsync = cancel => Task.CompletedTask },
        new TiVoCommand("Disabled") { ExecuteAsync = cancel => Task.CompletedTask },
        new TiVoCommand("MissingExecAsyncCmd") { IsEnabled = true },
        new TiVoCommand("VolumeUp", reverse: "VolumeDown") { IsEnabled = true, ExecuteAsync = cancel => Task.CompletedTask },
        new TiVoCommand("VolumeDown", reverse: "VolumeUp") { IsEnabled = true, ExecuteAsync = cancel => Task.CompletedTask },
        new TiVoCommand("CommandWithInvalidReverse", reverse: "InvalidReverse") { IsEnabled = true, ExecuteAsync = cancel => Task.CompletedTask },
    }.ToDictionary(x => x.Name);

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
        => $"Information[1301]: {LoggingMessages.ConversationState_Updated.AsMessageTemplate(state)}";
    private static string ExpectMessage_UnexpectedSpeechDetected(PhraseKinds unexpected, IRecognizedSpeech speech)
        => $"Error[1302]: {LoggingMessages.ConversationState_UnexpectedSpeechDetected.AsMessageTemplate(unexpected, speech)}";
    private static string ExpectMessage_Recognized(string text, string semantics)
        => $"Information[207]: {string.Format(LoggingMessages.ConversationController_Recognized, text, semantics)}";
    private static string ExpectMessage_UnknownCommand(string command)
        => $"Error[208]: {string.Format(LoggingMessages.ConversationController_UnknownCommand, command)}";
    private static string ExpectMessage_CommandMissingExecuteAction(string command)
        => $"Error[213]: {string.Format(LoggingMessages.ConversationController_CommandMissingExecuteAction, command)}";
    private static string ExpectMessage_CommandDisabled(string command)
        => $"Error[214]: {string.Format(LoggingMessages.ConversationController_CommandDisabled, command)}";
    private static string ExpectMessage_InvalidSemanticValue(string semanticKey, string invalidValue)
        => $"Warning[1303]: {LoggingMessages.ConversationState_InvalidSemanticValue.AsMessageTemplate(semanticKey, invalidValue)}";
    private static string ExpectMessage_UserReportedRecognitionError(IRecognizedSpeech speech)
        => $"Error[1304]: {LoggingMessages.ConversationState_UserReportedRecognitionError.AsMessageTemplate(speech)}";
    private static string ExpectMessage_CouldNotFindReverseCommand(Command command)
        => $"Error[1305]: {LoggingMessages.ConversationState_CouldNotFindReverseCommand.AsMessageTemplate(command, command.Reverse)}";

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
        Command expectedCommand = MockCommands["Play"];
        IRecognizedSpeech input = CreateMockSpeech(expectedCommand.Name, new()
        {
            ["command"] = expectedCommand.Name
        });

        ConversationState sut = new(MockCommands, WantsPhrases: PhraseKinds.Commands);

        ConversationState expected = sut with
        {
            LastCommand = input,
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Correction,
            LastResponse = new([expectedCommand.SpeakPhrase], [expectedCommand])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_Recognized(input.Text, "Play"),
            ExpectMessage_Updated(expected));
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_RepeatedCommand_ReturnsCommandMultipleTimes()
    {
        // Arrange
        Command expectedCommand = MockCommands["Play"];
        IRecognizedSpeech input = CreateMockSpeech(expectedCommand.Name, new()
        {
            ["command"] = expectedCommand.Name,
            ["repeat"] = "3"
        });

        ConversationState sut = new(MockCommands, WantsPhrases: PhraseKinds.Commands);

        ConversationState expected = sut with
        {
            LastCommand = input,
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Correction,
            LastResponse = new([Phrases.RepeatAction(expectedCommand.SpeakPhrase, 3)], [expectedCommand, expectedCommand, expectedCommand])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_Recognized(input.Text, "Play"),
            ExpectMessage_Updated(expected));
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_RepeatedCommand_WithInvalidRepeat_ReturnsCommandMultipleTimes()
    {
        // Arrange
        Command expectedCommand = MockCommands["Play"];
        IRecognizedSpeech input = CreateMockSpeech(expectedCommand.Name, new()
        {
            ["command"] = expectedCommand.Name,
            ["repeat"] = "The United States of America"
        });

        ConversationState sut = new(MockCommands, WantsPhrases: PhraseKinds.Commands);

        ConversationState expected = sut with
        {
            LastCommand = input,
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Correction,
            LastResponse = new([expectedCommand.SpeakPhrase], [expectedCommand])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_Recognized(input.Text, "Play"),
            ExpectMessage_InvalidSemanticValue("repeat", "The United States of America"),
            ExpectMessage_Updated(expected));
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_SimpleCommand_Disabled_RejectsAndLogs()
    {
        // Arrange
        Command expectedCommand = MockCommands.Values.Where(x => x.IsEnabled == false).First();
        IRecognizedSpeech lastCommand = CreateMockSpeech("Play");

        IRecognizedSpeech input = CreateMockSpeech("Disabled Command", new()
        {
            ["command"] = expectedCommand.Name
        });

        ConversationState sut = new(MockCommands, LastCommand: lastCommand, WantsPhrases: PhraseKinds.CommandsAndConfirmation);

        ConversationState expected = sut with
        {
            WantsPhrases = PhraseKinds.Commands,
            LastCommand = null,
            LastResponse = new([Phrases.Conversation_CommandDisabled(expectedCommand.Name)], [])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_CommandDisabled(expectedCommand.Name),
            ExpectMessage_Updated(expected));
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_SimpleCommand_MissingExecuteAsync_RejectsAndLogs()
    {
        // Arrange
        Command expectedCommand = MockCommands.Values.Where(x => x.ExecuteAsync is null).First();
        IRecognizedSpeech lastCommand = CreateMockSpeech("Play");

        IRecognizedSpeech input = CreateMockSpeech("Disabled Command", new()
        {
            ["command"] = expectedCommand.Name
        });

        ConversationState sut = new(MockCommands, LastCommand: lastCommand, WantsPhrases: PhraseKinds.CommandsAndConfirmation);

        ConversationState expected = sut with
        {
            WantsPhrases = PhraseKinds.Commands,
            LastCommand = null,
            LastResponse = new([Phrases.Conversation_CommandDisabled(expectedCommand.Name)], [])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_CommandMissingExecuteAction(expectedCommand.Name),
            ExpectMessage_Updated(expected));
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_SimpleCommand_WhenDoesntWantCommands_ReturnsCommandAndUpdatesState()
    {
        // Arrange
        IRecognizedSpeech input = CreateMockSpeech("Play", new()
        {
            ["command"] = "Play"
        });

        ConversationState sut = new(MockCommands, WantsPhrases: PhraseKinds.WakeWord);

        ConversationState expected = sut with
        {
            LastResponse = new([], [])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_UnexpectedSpeechDetected(PhraseKinds.Commands, input),
            ExpectMessage_Updated(expected));
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_Correction_ApologizesAndLogsError()
    {
        // Arrange
        IRecognizedSpeech previous = CreateMockSpeech("Previous", new());
        IRecognizedSpeech input = CreateMockSpeech("That's wrong", new()
        {
            ["correction"] = "true",
        });

        Command nonReversableCommand = MockCommands.Values.First(x => x.Reverse is null);
        ConversationState sut = new(MockCommands, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Correction, LastCommand: previous,
            LastResponse: new([], [nonReversableCommand]));

        ConversationState expected = sut with
        {
            LastCommand = input,
            LastResponse = new([Phrases.Conversation_ImSorry], [])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_UserReportedRecognitionError(previous),
            ExpectMessage_Updated(expected));
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_Correction_WhenDoesntWantCorrection_LogsError()
    {
        // Arrange
        IRecognizedSpeech input = CreateMockSpeech("That's wrong", new()
        {
            ["correction"] = "true",
        });

        ConversationState sut = new(MockCommands, WantsPhrases: PhraseKinds.Commands);

        ConversationState expected = sut with
        {
            LastResponse = new([], [])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_UnexpectedSpeechDetected(PhraseKinds.Correction, input),
            ExpectMessage_Updated(expected));
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_Correction_LogsErrorForCommandWithInvalidReverse()
    {
        // Arrange
        IRecognizedSpeech previous = CreateMockSpeech("Previous", new());
        IRecognizedSpeech input = CreateMockSpeech("That's wrong", new()
        {
            ["correction"] = "true",
        });

        Command commandWithInvalidReverse = MockCommands["CommandWithInvalidReverse"];
        ConversationState sut = new(MockCommands, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Correction, LastCommand: previous,
            LastResponse: new([], [commandWithInvalidReverse]));

        ConversationState expected = sut with
        {
            LastCommand = input,
            LastResponse = new([Phrases.Conversation_ImSorry], [])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_UserReportedRecognitionError(previous),
            ExpectMessage_CouldNotFindReverseCommand(commandWithInvalidReverse),
            ExpectMessage_Updated(expected));
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_Correction_ReversesReversableCommands()
    {
        // Arrange
        IRecognizedSpeech previous = CreateMockSpeech("Volume up 3 times", new()
        {
            ["command"] = "VolumeUp",
            ["repeat"] = "3"
        });

        IRecognizedSpeech input = CreateMockSpeech("That's wrong", new()
        {
            ["correction"] = "true",
        });

        Command commandToReverse = MockCommands.Values.First(x => x.Reverse is not null);
        Command reverseCommand = MockCommands[commandToReverse.Reverse!];

        ConversationState sut = new(MockCommands, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Correction, LastCommand: previous,
            LastResponse: new([], [commandToReverse, commandToReverse, commandToReverse]));

        ConversationState expected = sut with
        {
            LastCommand = input,
            LastResponse = new(
                [Phrases.Conversation_ImSorry, Phrases.RepeatAction(reverseCommand.SpeakPhrase, 3)],
                [reverseCommand, reverseCommand, reverseCommand])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_UserReportedRecognitionError(previous),
            ExpectMessage_Updated(expected));
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_Correction_ReversesDifferentReversableCommands()
    {
        // Arrange
        IRecognizedSpeech previous = CreateMockSpeech("Volume up and down", new()
        {
            ["command"] = "VolumeDown",
            ["repeat"] = "1"
        });

        IRecognizedSpeech input = CreateMockSpeech("That's wrong", new()
        {
            ["correction"] = "true",
        });

        Command commandToReverse = MockCommands.Values.First(x => x.Reverse is not null);
        Command reverseCommand = MockCommands[commandToReverse.Reverse!];

        ConversationState sut = new(MockCommands, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Correction, LastCommand: previous,
            LastResponse: new([], [commandToReverse, commandToReverse, reverseCommand]));

        ConversationState expected = sut with
        {
            LastCommand = input,
            LastResponse = new(
                [Phrases.Conversation_ImSorry, commandToReverse.SpeakPhrase, Phrases.RepeatAction(reverseCommand.SpeakPhrase, 2)],
                [commandToReverse, reverseCommand, reverseCommand])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(
            ExpectMessage_UserReportedRecognitionError(previous),
            ExpectMessage_Updated(expected));
    }
}
