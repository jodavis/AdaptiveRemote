using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Conversation;

internal static class ConversationStateExtensions
{
    public static (ConversationState, ConversationResponse) RespondTo(this ConversationState state, IRecognizedSpeech speech, ILogger logger)
    {
        List<string> phrases = new();
        List<Command> commands = new();

        if (speech.TryGetSemanticValue("system", out string? systemCommand))
        {
            switch (systemCommand)
            {
                // TODO: Only do this if wants WakeWords
                case "STARTLISTENING":
                    state = state with { WantsPhrases = PhraseKinds.Commands, LastCommand = default };
                    phrases.Add(Phrases.Conversation_ImListening);
                    break;

                // TODO: Only do tis if wants Commands
                case "STOPLISTENING":
                    state = state with { WantsPhrases = PhraseKinds.WakeWord, LastCommand = default };
                    phrases.Add(speech.ContainsSemanticValue("thankyou")
                        ? Phrases.Conversation_YoureWelcome
                        : Phrases.Conversation_StoppedListening);
                    break;
            }
        }

        // TODO: Only do this if wants commands
        if (speech.TryGetSemanticValue("command", out string? command))
        {
            // TODO: Support repeat
            // TODO: Handle missing Execute message
            // TODO: Handle disabled

            logger.LogInformation(Message.ConversationController_Recognized, speech.Text, command);
            state = state with
            {
                WantsPhrases = PhraseKinds.Commands | PhraseKinds.Correction,
                LastCommand = speech
            };
            phrases.Add(state.Commands[command].SpeakPhrase);
            commands.Add(state.Commands[command]);
        }
        else
        {
            // TODO: Handle unknown command
        }

        logger.LogInformation(Message.ConverationState_Updated, state);
        return (state, new(phrases, commands));
    }
}
