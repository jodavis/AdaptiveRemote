using System.Collections.Immutable;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Conversation;

internal static class ConversationStateExtensions
{
    public static ConversationState ToggleListening(this ConversationState state, ILogger? logger = default)
        => (state.WantsPhrases == PhraseKinds.WakeWord
            ? state with
            {
                WantsPhrases = PhraseKinds.Commands,
                CurrentResponse = new([], []),
                LastSpeech = default
            }
            : state with
            {
                WantsPhrases = PhraseKinds.WakeWord,
                CurrentResponse = new([], []),
                LastSpeech = default
            }).LogUpdateTo(logger);

    public static ConversationState RespondTo(this ConversationState state, IRecognizedSpeech speech, ILogger? logger = default)
    {
        RespondContext context = new(state, speech, logger);

        context = context.RespondTo();

        ConversationResponse response = new(context.ResponsePhrases, context.ResponseCommands);

        // Put more common logic here, e.g. WantsPhrases determined by other values
        state = context.State with
        {
            LastSpeech = speech,
            LastResponseWithCommands = response.Commands.Any() ? response : state.LastResponseWithCommands,
            CurrentResponse = response,
            WantsPhrases = context.State.WantsPhrases.HasFlag(PhraseKinds.Commands)
                ? PhraseKinds.Commands
                    | (response.Commands.Any() || context.State.LastResponseWithCommands is not null ? PhraseKinds.Correction : PhraseKinds.None)
                    | (SpeechIsHighConfidence(context) ? PhraseKinds.None : PhraseKinds.Confirmation)
                : context.State.WantsPhrases
        };

        return state.LogUpdateTo(logger);
    }

    private static RespondContext RespondTo(this RespondContext context)
    {
        if (context.Speech.TryGetSemanticValue("confirmation", out string? confirmationValue))
        {
            context = context
                .StopUnless(PhraseKindIsAccepted(PhraseKinds.Confirmation))
                .StopUnless(ConfirmationIsAffirmative(confirmationValue), ifStopped: RejectLastSpeech)
                .Apply(ConfirmLastCommand);
        }

        if (context.Speech.TryGetSemanticValue("system", out string? systemCommand))
        {
            switch (systemCommand)
            {
                case "STARTLISTENING":
                    context = context
                        .StopUnless(PhraseKindIsAccepted(PhraseKinds.WakeWord))
                        .Apply(EnterListeningState);
                    break;

                case "STOPLISTENING":
                    context = context
                        .StopUnless(PhraseKindIsAccepted(PhraseKinds.Commands))
                        .StopUnless(SpeechIsHighConfidence, ifStopped: AskForConfirmation)
                        .Apply(ExitListeningState);
                    break;
            }
        }

        if (context.Speech.TryGetSemanticValue("command", out string? commandName))
        {
            context = context
                .StopUnless(PhraseKindIsAccepted(PhraseKinds.Commands))
                .Apply(DecodeCommandFor(commandName))
                .StopUnless(CommandExists)
                .StopUnless(SpeechIsHighConfidence, ifStopped: AskForConfirmation)
                .StopUnless(CommandEnabled, ifStopped: RespondCommandDisabled)
                .StopUnless(CommandHasExecuteAsync, ifStopped: RespondCommandDisabled)
                .Apply(AddCommands);
        }

        if (context.Speech.TryGetSemanticValue("correction", out _))
        {
            context = context
                .StopUnless(PhraseKindIsAccepted(PhraseKinds.Correction))
                .StopUnless(SpeechIsHighConfidence, ifStopped: AskForConfirmation)
                .Apply(ApologizeFor(context.State.LastCommand!))
                .Apply(ReverseLastCommands);
        }

        return context;
    }

    private static bool SpeechIsHighConfidence(RespondContext context)
        => context.SpeechConfirmed || context.Speech.Confidence >= context.State.HighConfidenceThreshold;

    private static RespondContext AskForConfirmation(RespondContext context)
        => context with
        {
            ResponsePhrases = context.ResponsePhrases.Add(Phrases.Conversation_DidYouSay(context.Speech.Text)),
            State = context.State with
            {
                WantsPhrases = PhraseKinds.Commands | PhraseKinds.Confirmation,
            }
        };

    private static Func<RespondContext, bool> ConfirmationIsAffirmative(string confirmationValue)
        => context => confirmationValue.Equals("YES", StringComparison.OrdinalIgnoreCase);

    private static RespondContext ConfirmLastCommand(RespondContext context)
        => context with
        {
            Speech = context.State.LastSpeech!,
            SpeechConfirmed = true,
        };

    private static RespondContext RejectLastSpeech(RespondContext context)
        => ApologizeFor(context.State.LastSpeech!)(context);

    private static Func<RespondContext, RespondContext> ApologizeFor(IRecognizedSpeech speech)
        => context =>
        {
            if (context.ResponsePhrases.Contains(Phrases.Conversation_ImSorry))
            {
                // Already apologized, don't do it again
                return context;
            }

            context.Logger?.LogError(Message.ConversationState_UserReportedRecognitionError, speech);
            return context with
            {
                ResponsePhrases = context.ResponsePhrases.Add(Phrases.Conversation_ImSorry)
            };
        };

    private static RespondContext ReverseLastCommands(RespondContext context)
    {
        if (context.State.LastResponseWithCommands is not null)
        {
            List<Command> reverseCommands = new();
            ;
            List<string> reversePhrases = new();
            Command? lastReverseCommand = null;
            int lastReverseCommandCount = 0;

            foreach (Command commandToReverse in context.State.LastResponseWithCommands.Commands.Reverse())
            {
                if (commandToReverse.Reverse is null)
                {
                    continue;
                }

                if (!context.State.Commands.TryGetValue(commandToReverse.Reverse, out Command? reverseCommand))
                {
                    context.Logger?.LogError(Message.ConversationState_CouldNotFindReverseCommand, commandToReverse, commandToReverse.Reverse);
                    continue;
                }

                reverseCommands.Add(reverseCommand);

                if (reverseCommand == lastReverseCommand)
                {
                    lastReverseCommandCount++;
                }
                else
                {
                    if (lastReverseCommand is not null)
                    {
                        reversePhrases.Add(Phrases.RepeatAction(lastReverseCommand.SpeakPhrase, lastReverseCommandCount));
                    }

                    lastReverseCommand = reverseCommand;
                    lastReverseCommandCount = 1;
                }
            }

            if (lastReverseCommand is not null)
            {
                reversePhrases.Add(Phrases.RepeatAction(lastReverseCommand.SpeakPhrase, lastReverseCommandCount));

                return context with
                {
                    ResponseCommands = context.ResponseCommands.AddRange(reverseCommands),
                    ResponsePhrases = context.ResponsePhrases.AddRange(reversePhrases),
                    State = context.State with
                    {
                        LastCommand = context.Speech,
                    }
                };
            }
        }

        return context;
    }

    private static RespondContext EnterListeningState(RespondContext context)
        => context with
        {
            ResponsePhrases = context.ResponsePhrases.Add(Phrases.Conversation_ImListening),
            State = context.State with
            {
                WantsPhrases = PhraseKinds.Commands,
                // TODO: LastResponseWithCommands = null wherever LastCommand = null
                LastCommand = default,
            }
        };

    private static RespondContext ExitListeningState(RespondContext context)
        => context with
        {
            ResponsePhrases = context.ResponsePhrases.Add(context.Speech.ContainsSemanticValue("thankyou") ? Phrases.Conversation_YoureWelcome : Phrases.Conversation_StoppedListening),
            State = context.State with
            {
                WantsPhrases = PhraseKinds.WakeWord,
                LastCommand = default,
            }
        };

    private static Func<RespondContext, bool> PhraseKindIsAccepted(PhraseKinds received)
        => context => context.State.WantsPhrases.HasFlag(received)
            .LogErrorIf(false, context.Logger, Message.ConversationState_UnexpectedSpeechDetected, received, context.Speech);

    private static Func<RespondContext, RespondContext> DecodeCommandFor(string commandName)
        => context => context.State.Commands.TryGetValue(commandName, out Command? command)
            ? context with { DecodedCommand = command }
            : context;

    private static ConversationState LogUpdateTo(this ConversationState state, ILogger? logger)
    {
        logger?.LogInformation(Message.ConversationState_Updated, state);
        return state;
    }

    private static TestType LogErrorIf<TestType>(this TestType checkValue, TestType equalsValue, ILogger? logger, Message message, params object?[] arguments)
        where TestType : struct, IEquatable<TestType>
    {
        if (logger is not null && checkValue.Equals(equalsValue))
        {
            logger.LogError(message, arguments);
        }

        return checkValue;
    }

    private static bool CommandExists(RespondContext context)
        => (context.DecodedCommand is not null)
            .LogErrorIf(false, context.Logger, Message.ConversationController_UnknownCommand, context.Speech.Text);

    private static bool CommandEnabled(RespondContext context)
        => (context.DecodedCommand?.IsEnabled == true)
            .LogErrorIf(false, context.Logger, Message.ConversationController_CommandDisabled, context.DecodedCommand?.Name);

    private static bool CommandHasExecuteAsync(RespondContext context)
        => (context.DecodedCommand?.ExecuteAsync is not null)
            .LogErrorIf(false, context.Logger, Message.ConversationController_CommandMissingExecuteAction, context.DecodedCommand?.Name);

    private static RespondContext RespondCommandDisabled(RespondContext context)
        => context with
        {
            ResponsePhrases = context.ResponsePhrases.Add(Phrases.Conversation_CommandDisabled(context.DecodedCommand!.Name)),
            State = context.State with
            {
                WantsPhrases = PhraseKinds.Commands,
                LastSpeech = default
            }
        };

    private static RespondContext AddCommands(RespondContext context)
    {
        Command command = context.DecodedCommand ?? throw new ArgumentNullException(nameof(context), nameof(context.DecodedCommand) + " should not be null");

        context.Logger?.LogInformation(Message.ConversationController_Recognized, context.Speech.Text, command.Name);

        int repeat = 1;
        if (context.Speech.TryGetSemanticValue("repeat", out string? repeatAsString))
        {
            if (!int.TryParse(repeatAsString, out repeat))
            {
                context.Logger?.LogWarning(Message.ConversationState_InvalidSemanticValue, "repeat", repeatAsString);
                repeat = 1;
            }
        }

        return context with
        {
            ResponsePhrases = context.ResponsePhrases.Add(Phrases.RepeatAction(command.SpeakPhrase, repeat)),
            ResponseCommands = context.ResponseCommands.AddRange(Enumerable.Repeat(command, repeat)),
            State = context.State with
            {
                WantsPhrases = PhraseKinds.Commands | PhraseKinds.Correction,
                LastCommand = context.Speech
            }
        };
    }

    private record RespondContext(
        ConversationState State,
        IRecognizedSpeech Speech,
        ImmutableList<string> ResponsePhrases,
        ImmutableList<Command> ResponseCommands,
        bool Continue,
        ILogger? Logger,
        Command? DecodedCommand = default,
        bool SpeechConfirmed = false)
    {
        internal RespondContext(ConversationState state, IRecognizedSpeech speech, ILogger? logger)
            : this(state, speech, ImmutableList<string>.Empty, ImmutableList<Command>.Empty, true, logger)
        { }

        internal RespondContext StopUnless(Func<RespondContext, bool> condition, Func<RespondContext, RespondContext>? ifStopped = default)
            => Continue
            ? condition(this) ? this : (ifStopped ?? DoNothing)(this with { Continue = false })
            : this;

        internal RespondContext Apply(Func<RespondContext, RespondContext> transform)
            => Continue ? transform(this) : this;

        private static RespondContext DoNothing(RespondContext context) => context;
    }
}
