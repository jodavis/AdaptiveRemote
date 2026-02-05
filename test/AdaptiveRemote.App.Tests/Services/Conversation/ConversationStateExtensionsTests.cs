using AdaptiveRemote.Models;
using Moq;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class ConversationStateExtensionsTests
{
    private const int TestConfidenceThreshold = 70;

    private readonly MockLogger<ConversationState> MockLogger = new();

    private static readonly IReadOnlyDictionary<string, Command> MockCommands = new List<Command>
    {
        new TiVoCommand("Play", speakName: "Play It!") { IsEnabled = true, ExecuteAsync = cancel => Task.CompletedTask },
        new TiVoCommand("Disabled") { ExecuteAsync = cancel => Task.CompletedTask },
        new TiVoCommand("MissingExecAsyncCmd") { IsEnabled = true },
        new TiVoCommand("VolumeUp", reverse: "VolumeDown") { IsEnabled = true, ExecuteAsync = cancel => Task.CompletedTask },
        new TiVoCommand("VolumeDown", reverse: "VolumeUp") { IsEnabled = true, ExecuteAsync = cancel => Task.CompletedTask },
        new TiVoCommand("CommandWithInvalidReverse", reverse: "InvalidReverse") { IsEnabled = true, ExecuteAsync = cancel => Task.CompletedTask },
    }.ToDictionary(x => x.Name);

    private static IRecognizedSpeech CreateMockSpeech(string text)
        => CreateMockSpeech(text, new());
    private static IRecognizedSpeech CreateMockSpeech(string text, Dictionary<string, string> semantics, int confidence = TestConfidenceThreshold)
    {
        Mock<IRecognizedSpeech> mockSpeech = new();
        mockSpeech
            .SetupGet(x => x.Text)
            .Returns(text);
        mockSpeech
            .SetupGet(x => x.Confidence)
            .Returns(confidence);

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

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_WakeWords_EntersListeningMode()
    {
        // Arrange
        IRecognizedSpeech lastCommand = CreateMockSpeech("Play");

        IRecognizedSpeech input = CreateMockSpeech("Hey Remote", new()
        {
            ["system"] = "STARTLISTENING"
        });

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.WakeWord, SpeechToConfirm: lastCommand, ResponseToCorrect: new(lastCommand, [], []));

        ConversationState expected = sut with
        {
            WantsPhrases = PhraseKinds.Commands,
            SpeechToConfirm = null,
            ResponseToCorrect = null,
            CurrentResponse = new(input, [Phrases.Conversation_ImListening], []),
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_Updated(expected);
        });
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

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.Commands);

        ConversationState expected = sut with
        {
            WantsPhrases = PhraseKinds.WakeWord,
            SpeechToConfirm = null,
            CurrentResponse = new(input, [Phrases.Conversation_StoppedListening], []),
            ResponseToCorrect = null,
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_Updated(expected);
        });
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_StopListening_WithLowConfidence_AsksForConfirmation()
    {
        // Arrange
        IRecognizedSpeech lastCommand = CreateMockSpeech("Play");

        IRecognizedSpeech input = CreateMockSpeech("Stop listening", new()
        {
            ["system"] = "STOPLISTENING"
        }, confidence: TestConfidenceThreshold - 1);

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.Commands);

        ConversationState expected = sut with
        {
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Confirmation,
            SpeechToConfirm = input,
            CurrentResponse = new(input, [Phrases.Conversation_DidYouSay(input.Text)], []),
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_Updated(expected);
        });
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

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.Commands, SpeechToConfirm: lastCommand, ResponseToCorrect: new(lastCommand, [], []));

        ConversationState expected = sut with
        {
            WantsPhrases = PhraseKinds.WakeWord,
            SpeechToConfirm = null,
            CurrentResponse = new(input, [Phrases.Conversation_YoureWelcome], []),
            ResponseToCorrect = null,
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_Updated(expected);
        });
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

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.Commands, SpeechToConfirm: input);

        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Correction,
            CurrentResponse = new(input, [expectedCommand.SpeakPhrase], [expectedCommand]),
            ResponseToCorrect = new(input, [expectedCommand.SpeakPhrase], [expectedCommand]),
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationController_Recognized(input.Text, "Play");
            log.ConversationState_Updated(expected);
        });
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

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.Commands);

        ConversationResponse expectedResponse = new(input, [Phrases.RepeatAction(expectedCommand.SpeakPhrase, 3)], [expectedCommand, expectedCommand, expectedCommand]);
        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Correction,
            CurrentResponse = expectedResponse,
            ResponseToCorrect = expectedResponse,
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationController_Recognized(input.Text, "Play");
            log.ConversationState_Updated(expected);
        });
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

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.Commands);

        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Correction,
            CurrentResponse = new(input, [expectedCommand.SpeakPhrase], [expectedCommand]),
            ResponseToCorrect = new(input, [expectedCommand.SpeakPhrase], [expectedCommand])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationController_Recognized(input.Text, "Play");
            log.ConversationState_InvalidSemanticValue("repeat", "The United States of America");
            log.ConversationState_Updated(expected);
        });
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

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, SpeechToConfirm: lastCommand, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Confirmation);

        ConversationState expected = sut with
        {
            WantsPhrases = PhraseKinds.Commands,
            SpeechToConfirm = null,
            CurrentResponse = new(input, [Phrases.Conversation_CommandDisabled(expectedCommand.Name)], []),
            ResponseToCorrect = null,
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationController_CommandDisabled(expectedCommand);
            log.ConversationState_Updated(expected);
        });
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

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, SpeechToConfirm: lastCommand, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Confirmation);

        ConversationState expected = sut with
        {
            WantsPhrases = PhraseKinds.Commands,
            SpeechToConfirm = null,
            CurrentResponse = new(input, [Phrases.Conversation_CommandDisabled(expectedCommand.Name)], []),
            ResponseToCorrect = null,
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationController_CommandMissingExecuteAction(expectedCommand);
            log.ConversationState_Updated(expected);
        });
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_SimpleCommand_WhenDoesntWantCommands_ReturnsCommandAndUpdatesState()
    {
        // Arrange
        IRecognizedSpeech input = CreateMockSpeech("Play", new()
        {
            ["command"] = "Play"
        });

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.WakeWord);

        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            CurrentResponse = new(input, [], [])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_UnexpectedSpeechDetected(PhraseKinds.Commands, input);
            log.ConversationState_Updated(expected);
        });
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
        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Correction,
            ResponseToCorrect: new(previous, [], [nonReversableCommand]));

        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            CurrentResponse = new(input, [Phrases.Conversation_ImSorry], []),
            ResponseToCorrect = null,
            WantsPhrases = PhraseKinds.Commands,
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_UserReportedRecognitionError(previous);
            log.ConversationState_Updated(expected);
        });
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_Correction_WhenDoesntWantCorrection_LogsError()
    {
        // Arrange
        IRecognizedSpeech input = CreateMockSpeech("That's wrong", new()
        {
            ["correction"] = "true",
        });

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.Commands);

        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            CurrentResponse = new(input, [], [])
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_UnexpectedSpeechDetected(PhraseKinds.Correction, input);
            log.ConversationState_Updated(expected);
        });
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
        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Correction,
            ResponseToCorrect: new(previous, [], [commandWithInvalidReverse]));

        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            CurrentResponse = new(input, [Phrases.Conversation_ImSorry], []),
            ResponseToCorrect = null,
            WantsPhrases = PhraseKinds.Commands
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_UserReportedRecognitionError(previous);
            log.ConversationState_CouldNotFindReverseCommand(commandWithInvalidReverse, commandWithInvalidReverse.Reverse);
            log.ConversationState_Updated(expected);
        });
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

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Correction,
            ResponseToCorrect: new(previous, [], [commandToReverse, commandToReverse, commandToReverse]));

        ConversationResponse expectedResponse = new(
            input,
            [Phrases.Conversation_ImSorry, Phrases.RepeatAction(reverseCommand.SpeakPhrase, 3)],
            [reverseCommand, reverseCommand, reverseCommand]);
        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            CurrentResponse = expectedResponse,
            ResponseToCorrect = expectedResponse,
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_UserReportedRecognitionError(previous);
            log.ConversationState_Updated(expected);
        });
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

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Correction,
            ResponseToCorrect: new(previous, [], [commandToReverse, commandToReverse, reverseCommand]));

        ConversationResponse expectedResponse = new(
            input,
            [Phrases.Conversation_ImSorry, commandToReverse.SpeakPhrase, Phrases.RepeatAction(reverseCommand.SpeakPhrase, 2)],
            [commandToReverse, reverseCommand, reverseCommand]);
        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            CurrentResponse = expectedResponse,
            ResponseToCorrect = expectedResponse,
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_UserReportedRecognitionError(previous);
            log.ConversationState_Updated(expected);
        });
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_Correction_WithLowConfidence_AsksForConfirmation()
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
        }, confidence: TestConfidenceThreshold - 1);

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Correction);

        ConversationResponse expectedResponse = new(input, [Phrases.Conversation_DidYouSay(input.Text)], []);
        ConversationState expected = sut with
        {
            SpeechToConfirm = input,
            CurrentResponse = expectedResponse,
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Confirmation
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_Updated(expected);
        });
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_SimpleCommand_WithLowConfidence_AsksForConfirmaion()
    {
        // Arrange
        IRecognizedSpeech input = CreateMockSpeech("Play", new()
        {
            ["command"] = "Play"
        }, confidence: TestConfidenceThreshold - 1);

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.Commands);

        ConversationState expected = sut with
        {
            SpeechToConfirm = input,
            CurrentResponse = new(input, [Phrases.Conversation_DidYouSay(input.Text)], []),
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Confirmation
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_Updated(expected);
        });
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_Confirmation_WhenNotExpected_RejectsSpeech()
    {
        // Arrange
        IRecognizedSpeech command = CreateMockSpeech("Play", new()
        {
            ["command"] = "Play"
        });

        IRecognizedSpeech input = CreateMockSpeech("Yes", new()
        {
            ["confirmation"] = "YES"
        });

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, SpeechToConfirm: command, CurrentResponse: new(input, [MockCommands["Play"].SpeakPhrase], [MockCommands["Play"]]), WantsPhrases: PhraseKinds.Commands);

        ConversationState expected = sut with
        {
            SpeechToConfirm = command,
            CurrentResponse = new(input, [], []),
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Confirmation,
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_UnexpectedSpeechDetected(PhraseKinds.Confirmation, input);
            log.ConversationState_Updated(expected);
        });
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_ConfirmationForCommand_Yes_ExecuetesPreviousCommand()
    {
        // Arrange
        IRecognizedSpeech command = CreateMockSpeech("Play", new()
        {
            ["command"] = "Play"
        }, confidence: TestConfidenceThreshold - 1);

        IRecognizedSpeech input = CreateMockSpeech("Yes", new()
        {
            ["confirmation"] = "YES"
        });

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, SpeechToConfirm: command, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Confirmation);

        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            CurrentResponse = new(input, [MockCommands["Play"].SpeakPhrase], [MockCommands["Play"]]),
            ResponseToCorrect = new(input, [MockCommands["Play"].SpeakPhrase], [MockCommands["Play"]]),
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Correction
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationController_Recognized("Play", "Play");
            log.ConversationState_Updated(expected);
        });
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_ConfirmationForCommand_No_Apologizes()
    {
        // Arrange
        IRecognizedSpeech command = CreateMockSpeech("Play", new()
        {
            ["command"] = "Play"
        }, confidence: TestConfidenceThreshold - 1);

        IRecognizedSpeech input = CreateMockSpeech("No, that's wrong", new()
        {
            ["confirmation"] = "NO"
        });

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, SpeechToConfirm: command, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Confirmation);

        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            CurrentResponse = new(input, [Phrases.Conversation_ImSorry], []),
            WantsPhrases = PhraseKinds.Commands
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_UserReportedRecognitionError(command);
            log.ConversationState_Updated(expected);
        });
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_ConfirmationForThatsWrong_Yes_ExecuetesPreviousCommand()
    {
        // Arrange
        Command reversableCommand = MockCommands.Values.First(x => x.Reverse is not null);
        Command reverseCommand = MockCommands[reversableCommand.Reverse!];
        IRecognizedSpeech command = CreateMockSpeech(reversableCommand.Name, new()
        {
            ["command"] = reversableCommand.Name
        });

        IRecognizedSpeech thatsWrong = CreateMockSpeech("That's wrong", new()
        {
            ["correction"] = "true"
        }, confidence: TestConfidenceThreshold - 1);

        IRecognizedSpeech input = CreateMockSpeech("Yes", new()
        {
            ["confirmation"] = "YES"
        });

        ConversationState sut = new(MockCommands, TestConfidenceThreshold,
            SpeechToConfirm: thatsWrong,
            WantsPhrases: PhraseKinds.Commands | PhraseKinds.Correction | PhraseKinds.Confirmation,
            ResponseToCorrect: new(command, [reversableCommand.SpeakPhrase], [reversableCommand]));

        ConversationResponse expectedResponse = new(input, [Phrases.Conversation_ImSorry, reverseCommand.SpeakPhrase], [reverseCommand]);
        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            CurrentResponse = expectedResponse,
            ResponseToCorrect = expectedResponse,
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Correction
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_UserReportedRecognitionError(command);
            log.ConversationState_Updated(expected);
        });
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_ConfirmationForThatsWrong_No_Apologizes()
    {
        // Arrange
        IRecognizedSpeech command = CreateMockSpeech("That's wrong", new()
        {
            ["correction"] = "true"
        }, confidence: TestConfidenceThreshold - 1);

        IRecognizedSpeech input = CreateMockSpeech("No, that's wrong", new()
        {
            ["confirmation"] = "NO"
        });

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, SpeechToConfirm: command, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Confirmation);

        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            CurrentResponse = new(input, [Phrases.Conversation_ImSorry], []),
            WantsPhrases = PhraseKinds.Commands
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_UserReportedRecognitionError(command);
            log.ConversationState_Updated(expected);
        });
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_ConfirmationForStopListening_Yes_ExitsListeningMode()
    {
        // Arrange
        IRecognizedSpeech thatsWrong = CreateMockSpeech("Stop listening", new()
        {
            ["system"] = "STOPLISTENING"
        }, confidence: TestConfidenceThreshold - 1);

        IRecognizedSpeech input = CreateMockSpeech("Yes", new()
        {
            ["confirmation"] = "YES"
        });

        ConversationState sut = new(MockCommands, TestConfidenceThreshold,
            SpeechToConfirm: thatsWrong,
            WantsPhrases: PhraseKinds.Commands | PhraseKinds.Confirmation);

        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            CurrentResponse = new(input, [Phrases.Conversation_StoppedListening], []),
            ResponseToCorrect = default,
            WantsPhrases = PhraseKinds.WakeWord
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_Updated(expected);
        });
    }

    [TestMethod]
    public void ConversationStateExtensions_RespondTo_ConfirmationForStopListening_No_Apologizes()
    {
        // Arrange
        IRecognizedSpeech command = CreateMockSpeech("Stop listening", new()
        {
            ["system"] = "STOPLISTENING"
        }, confidence: TestConfidenceThreshold - 1);

        IRecognizedSpeech input = CreateMockSpeech("No, that's wrong", new()
        {
            ["confirmation"] = "NO"
        });

        ConversationState sut = new(MockCommands, TestConfidenceThreshold, SpeechToConfirm: command, WantsPhrases: PhraseKinds.Commands | PhraseKinds.Confirmation);

        ConversationState expected = sut with
        {
            SpeechToConfirm = null,
            CurrentResponse = new(input, [Phrases.Conversation_ImSorry], []),
            WantsPhrases = PhraseKinds.Commands
        };

        // Act
        ConversationState result = sut.RespondTo(input, MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_UserReportedRecognitionError(command);
            log.ConversationState_Updated(expected);
        });
    }

    // TODO: Low confidence for other phrases
    //  Confirmation?

    [TestMethod]
    public void ConversationStateExtensions_ToggleListening_WhenWaitingForWakeWord_EntersListening()
    {
        // Arrange
        ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: PhraseKinds.WakeWord, SpeechToConfirm: CreateMockSpeech("What?!?"));

        ConversationState expected = sut with
        {
            WantsPhrases = PhraseKinds.Commands,
            CurrentResponse = null,
            SpeechToConfirm = null
        };

        // Act
        ConversationState result = sut.ToggleListening(MockLogger);

        // Assert
        Assert.AreEqual(expected, result, nameof(result));

        MockLogger.VerifyMessages(log =>
        {
            log.ConversationState_Updated(expected);
        });
    }

    [TestMethod]
    public void ConversationStateExtensions_ToggleListening_WhenWaitingForAnythingButWakeWord_ExitsListening()
    {
        for (int i = 1; i < (int)PhraseKinds.All; i++)
        {
            if (i == (int)PhraseKinds.WakeWord)
            {
                continue;
            }

            // Arrange
            ConversationState sut = new(MockCommands, TestConfidenceThreshold, WantsPhrases: (PhraseKinds)i, SpeechToConfirm: CreateMockSpeech("What?!?"));

            ConversationState expected = sut with
            {
                WantsPhrases = PhraseKinds.WakeWord,
                SpeechToConfirm = null,
                CurrentResponse = null,
            };

            // Act
            ConversationState result = sut.ToggleListening(MockLogger);

            // Assert
            Assert.AreEqual(expected, result, nameof(result) + " when WantsPhrases started as {0}", (PhraseKinds)i);

            MockLogger.VerifyMessages(log =>
            {
                log.ConversationState_Updated(expected);
            });
            MockLogger.ClearMessages();
        }
    }
}
