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
                CurrentResponse = null,
                SpeechToConfirm = default
            }
            : state with
            {
                WantsPhrases = PhraseKinds.WakeWord,
                CurrentResponse = null,
                SpeechToConfirm = default
            }).LogUpdateTo(logger is null ? null : new(logger));

    public static ConversationState RespondTo(this ConversationState state, IRecognizedSpeech speech, ILogger? logger = default)
    {
        RespondContext context = new(state, speech, logger is null ? null : new(logger));

        context = context.RespondTo();

        ConversationResponse response = new(speech, context.ResponsePhrases, context.ResponseCommands);

        state = context.State with
        {
            ResponseToCorrect = response.Commands.Any() ? response : context.State.ResponseToCorrect,
            CurrentResponse = response,
            WantsPhrases = ComputePhraseKinds(context)
        };

        return state.LogUpdateTo(context.Logger);

        static PhraseKinds ComputePhraseKinds(RespondContext context)
        {
            PhraseKinds computed = context.State.WantsPhrases;

            if (computed.HasFlag(PhraseKinds.Commands))
            {
                if (context.ResponseCommands.IsEmpty)
                {
                    computed &= ~PhraseKinds.Correction;
                }
                else
                {
                    computed |= PhraseKinds.Correction;
                }

                if (context.State.SpeechToConfirm is not null)
                {
                    computed |= PhraseKinds.Confirmation;
                }
                else
                {
                    computed &= ~PhraseKinds.Confirmation;
                }
            }

            return computed;
        }
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
                .StopUnless(CommandHasExecuted, ifStopped: RespondCommandDisabled)
                .Apply(AddCommands)
                .Apply(SpeakDescriptionOfCommands);
        }

        if (context.Speech.TryGetSemanticValue("correction", out _))
        {
            context = context
                .StopUnless(PhraseKindIsAccepted(PhraseKinds.Correction))
                .StopUnless(SpeechIsHighConfidence, ifStopped: AskForConfirmation)
                .Apply(ApologizeFor(context.State.ResponseToCorrect?.InResponseTo!))
                .Apply(ReverseLastCommands)
                .Apply(SpeakDescriptionOfCommands);
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
                SpeechToConfirm = context.Speech
            }
        };

    private static Func<RespondContext, bool> ConfirmationIsAffirmative(string confirmationValue)
        => context => confirmationValue.Equals("YES", StringComparison.OrdinalIgnoreCase);

    private static RespondContext ConfirmLastCommand(RespondContext context)
        => context with
        {
            Speech = context.State.SpeechToConfirm!,
            SpeechConfirmed = true,
            State = context.State with
            {
                SpeechToConfirm = null
            }
        };

    private static RespondContext RejectLastSpeech(RespondContext context)
        => ApologizeFor(context.State.SpeechToConfirm!)(context) with
        {
            State = context.State with
            {
                SpeechToConfirm = null
            }
        };

    private static Func<RespondContext, RespondContext> ApologizeFor(IRecognizedSpeech speech)
        => context =>
        {
            if (context.ResponsePhrases.Contains(Phrases.Conversation_ImSorry))
            {
                // Already apologized, don't do it again
                return context;
            }

            context.Logger?.ConversationState_UserReportedRecognitionError(speech);
            return context with
            {
                ResponsePhrases = context.ResponsePhrases.Add(Phrases.Conversation_ImSorry)
            };
        };

    private static RespondContext ReverseLastCommands(RespondContext context)
    {
        ImmutableList<Command> reverseCommands = context.ResponseCommands;

        if (context.State.ResponseToCorrect is not null)
        {

            foreach (Command commandToReverse in context.State.ResponseToCorrect.Commands.Reverse())
            {
                if (commandToReverse.Reverse is null)
                {
                    continue;
                }

                if (!context.State.Commands.TryGetValue(commandToReverse.Reverse, out Command? reverseCommand))
                {
                    context.Logger?.ConversationState_CouldNotFindReverseCommand(commandToReverse, commandToReverse.Reverse);
                    continue;
                }

                reverseCommands = reverseCommands.Add(reverseCommand);
            }

            return context with
            {
                ResponseCommands = reverseCommands,
                State = context.State with
                {
                    ResponseToCorrect = null
                }
            };
        }

        return context;
    }

    private static RespondContext SpeakDescriptionOfCommands(RespondContext context)
    {
        ImmutableList<string> phrases = context.ResponsePhrases;
        Command? lastCommand = null;
        int lastCommandCount = 0;

        foreach (Command command in context.ResponseCommands)
        {
            if (lastCommand == command)
            {
                lastCommandCount++;
            }
            else
            {
                phrases = AddDescriptionOfCommand(phrases, lastCommand, lastCommandCount);

                lastCommand = command;
                lastCommandCount = 1;
            }
        }

        phrases = AddDescriptionOfCommand(phrases, lastCommand, lastCommandCount);

        return context.ResponsePhrases == phrases
            ? context
            : context with { ResponsePhrases = phrases };

        static ImmutableList<string> AddDescriptionOfCommand(ImmutableList<string> phrases, Command? command, int repeat)
            => command is null
            ? phrases
            : phrases.Add(Phrases.RepeatAction(command.SpeakPhrase, repeat));
    }

    private static RespondContext EnterListeningState(RespondContext context)
        => context with
        {
            ResponsePhrases = context.ResponsePhrases.Add(Phrases.Conversation_ImListening),
            State = context.State with
            {
                WantsPhrases = PhraseKinds.Commands,
                SpeechToConfirm = default,
                ResponseToCorrect = default,
            }
        };

    private static RespondContext ExitListeningState(RespondContext context)
        => context with
        {
            ResponsePhrases = context.ResponsePhrases.Add(context.Speech.ContainsSemanticValue("thankyou") ? Phrases.Conversation_YoureWelcome : Phrases.Conversation_StoppedListening),
            State = context.State with
            {
                WantsPhrases = PhraseKinds.WakeWord,
                SpeechToConfirm = default,
                ResponseToCorrect = default,
            }
        };

    private static Func<RespondContext, bool> PhraseKindIsAccepted(PhraseKinds received)
        => context => context.State.WantsPhrases.HasFlag(received)
            .LogErrorIf(false, context.Logger, m => m.ConversationState_UnexpectedSpeechDetected(received, context.Speech));

    private static Func<RespondContext, RespondContext> DecodeCommandFor(string commandName)
        => context => context.State.Commands.TryGetValue(commandName, out Command? command)
            ? context with { DecodedCommand = command }
            : context;

    private static ConversationState LogUpdateTo(this ConversationState state, MessageLogger? logger)
    {
        logger?.ConversationState_Updated(state);
        return state;
    }

    private static TestType LogErrorIf<TestType>(this TestType checkValue, TestType equalsValue, MessageLogger? logger, Action<MessageLogger> logErrorMessage)
        where TestType : struct, IEquatable<TestType>
    {
        if (logger is not null && checkValue.Equals(equalsValue))
        {
            logErrorMessage(logger);
        }

        return checkValue;
    }

    private static bool CommandExists(RespondContext context)
        => (context.DecodedCommand is not null)
            .LogErrorIf(false, context.Logger, m => m.ConversationController_UnknownCommand(context.Speech.Text));

    private static bool CommandEnabled(RespondContext context)
        => (context.DecodedCommand?.IsEnabled == true)
            .LogErrorIf(false, context.Logger, m => m.ConversationController_CommandDisabled(context.DecodedCommand));

    private static bool CommandHasExecuted(RespondContext context)
        => (context.DecodedCommand?.ExecuteAsync is not null)
            .LogErrorIf(false, context.Logger, m => m.ConversationController_CommandMissingExecuteAction(context.DecodedCommand));

    private static RespondContext RespondCommandDisabled(RespondContext context)
        => context with
        {
            ResponsePhrases = context.ResponsePhrases.Add(Phrases.Conversation_CommandDisabled(context.DecodedCommand!.Name)),
            State = context.State with
            {
                WantsPhrases = PhraseKinds.Commands,
                SpeechToConfirm = default
            }
        };

    private static RespondContext AddCommands(RespondContext context)
    {
        Command command = context.DecodedCommand ?? throw new ArgumentNullException(nameof(context), nameof(context.DecodedCommand) + " should not be null");

        context.Logger?.ConversationController_Recognized(context.Speech.Text, command.Name);

        int repeat = 1;
        if (context.Speech.TryGetSemanticValue("repeat", out string? repeatAsString))
        {
            if (!int.TryParse(repeatAsString, out repeat))
            {
                context.Logger?.ConversationState_InvalidSemanticValue("repeat", repeatAsString);
                repeat = 1;
            }
        }

        return context with
        {
            ResponseCommands = context.ResponseCommands.AddRange(Enumerable.Repeat(command, repeat)),
            State = context.State with
            {
                SpeechToConfirm = default
            }
        };
    }

    private record RespondContext(
        ConversationState State,
        IRecognizedSpeech Speech,
        ImmutableList<string> ResponsePhrases,
        ImmutableList<Command> ResponseCommands,
        bool Continue,
        MessageLogger? Logger,
        Command? DecodedCommand = default,
        bool SpeechConfirmed = false)
    {
        internal RespondContext(ConversationState state, IRecognizedSpeech speech, MessageLogger? logger)
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
