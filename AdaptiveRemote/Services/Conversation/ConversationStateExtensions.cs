using System.Diagnostics.CodeAnalysis;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Conversation;

internal static class ConversationStateExtensions
{
    public static ConversationState RespondTo(this ConversationState state, IRecognizedSpeech speech, ILogger? logger = default)
    {
        List<string> phrases = new();
        List<Command> commands = new();

        state = state with { LastResponse = new(new List<string>(), new List<Command>()) };

        if (speech.TryGetSemanticValue("system", out string? systemCommand))
        {
            switch (systemCommand)
            {
                case "STARTLISTENING":
                    state = state.RejectUnexpectedPhraseKind(PhraseKinds.WakeWord, speech, logger)
                        ?? state.EnterListingState();
                    break;

                case "STOPLISTENING":
                    state = state.RejectUnexpectedPhraseKind(PhraseKinds.Commands, speech, logger)
                        ?? state.ExitListingState(speech);
                    break;
            }
        }

        if (speech.TryGetSemanticValue("command", out string? commandName))
        {
            state = state.AddCommands(commandName, speech, logger);
        }

        logger?.LogInformation(Message.ConversationState_Updated, state);
        return state;
    }

    private static ConversationState AddCommands(this ConversationState state, string commandName, IRecognizedSpeech speech, ILogger? logger)
    {
        return state.RejectUnexpectedPhraseKind(PhraseKinds.Commands, speech, logger)
            ?? state.TryGetCommandFor(commandName, logger, out Command? command)
            ?? state.RejectDisabledCommand(command, logger)
            ?? state.RejectCommandWithoutExecuteAsync(command, logger)
            ?? state.AddCommands(speech, command, logger);
    }

    private static ConversationState EnterListingState(this ConversationState state)
    {
        List<string> phrases = (List<string>)state.LastResponse!.Phrases;
        phrases.Add(Phrases.Conversation_ImListening);

        return state with
        {
            WantsPhrases = PhraseKinds.Commands,
            LastCommand = default
        };
    }

    private static ConversationState ExitListingState(this ConversationState state, IRecognizedSpeech speech)
    {
        List<string> phrases = (List<string>)state.LastResponse!.Phrases;
        phrases.Add(speech.ContainsSemanticValue("thankyou")
            ? Phrases.Conversation_YoureWelcome
            : Phrases.Conversation_StoppedListening);

        return state with
        {
            WantsPhrases = PhraseKinds.WakeWord,
            LastCommand = default
        };
    }

    private static ConversationState? RejectUnexpectedPhraseKind(this ConversationState state, PhraseKinds received, IRecognizedSpeech speech, ILogger? logger)
    {
        if (state.WantsPhrases.HasFlag(received))
        {
            return default; // continue
        }
        else
        {
            logger?.LogWarning(Message.ConversationState_UnexpectedSpeechDetected, PhraseKinds.Commands, speech);
            return state;
        }
    }

    private static ConversationState? TryGetCommandFor(this ConversationState state, string commandName, ILogger? logger, [NotNull] out Command? command)
    {
        if (state.Commands.TryGetValue(commandName, out command))
        {
            return default; // contniue
        }
        else
        {
            command = null!;
            logger?.LogError(Message.ConversationController_UnknownCommand, commandName);
            return state;
        }
    }

    private static ConversationState? RejectDisabledCommand(this ConversationState state, Command command, ILogger? logger)
    {
        if (command.IsEnabled)
        {
            return default; // continue
        }
        else
        {
            List<string> phrases = (List<string>)state.LastResponse!.Phrases;

            logger?.LogError(Message.ConversationController_CommandDisabled, command.Name);
            phrases.Add(Phrases.Conversation_CommandDisabled(command.Name));
            return state with
            {
                WantsPhrases = PhraseKinds.Commands,
                LastCommand = default
            };
        }
    }

    private static ConversationState? RejectCommandWithoutExecuteAsync(this ConversationState state, Command command, ILogger? logger)
    {
        if (command.ExecuteAsync is not null)
        {
            return default; // continue
        }
        else
        {
            List<string> phrases = (List<string>)state.LastResponse!.Phrases;

            logger?.LogError(Message.ConversationController_CommandMissingExecuteAction, command.Name);
            phrases.Add(Phrases.Conversation_CommandDisabled(command.Name));
            return state with
            {
                WantsPhrases = PhraseKinds.Commands,
                LastCommand = default
            };
        }
    }

    private static ConversationState AddCommands(this ConversationState state, IRecognizedSpeech speech, Command command, ILogger? logger)
    {
        List<string> phrases = (List<string>)state.LastResponse!.Phrases;
        List<Command> commands = (List<Command>)state.LastResponse!.Commands;

        logger?.LogInformation(Message.ConversationController_Recognized, speech.Text, command.Name);

        int repeat = 1;
        if (speech.TryGetSemanticValue("repeat", out string? repeatAsString))
        {
            if (!int.TryParse(repeatAsString, out repeat))
            {
                logger?.LogWarning(Message.ConversationState_InvalidSemanticValue, "repeat", repeatAsString);
                repeat = 1;
            }
        }

        phrases.Add(Phrases.RepeatAction(command.SpeakPhrase, repeat));
        commands.AddRange(Enumerable.Repeat(command, repeat));

        return state with
        {
            WantsPhrases = PhraseKinds.Commands | PhraseKinds.Correction,
            LastCommand = speech
        };
    }
}
