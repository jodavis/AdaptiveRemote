using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Conversation;

internal class ConversationStateMachine
{
    private ConversationState _state;

    public ConversationStateMachine(IRemoteDefinitionService definitionService, ILogger<ConversationStateMachine> logger)
    {
        Logger = logger;

        _state = new(GetCommands(definitionService), WantsPhrases: PhraseKinds.WakeWord);
    }

    public ILogger Logger { get; }
    public PhraseKinds WantPhrases => _state.WantsPhrases;

    internal ConversationResponse RespondTo(IRecognizedSpeech result)
    {
        return (_state = _state.RespondTo(result, Logger)).LastResponse
            ?? throw new Exception("State machine did not produce a ConversationResponse");
    }

    private static IReadOnlyDictionary<string, Command> GetCommands(IRemoteDefinitionService definitionService)
    {
        Dictionary<string, Command> commands = new(StringComparer.Ordinal);

        foreach (Command command in definitionService.GetCommands())
        {
            commands[command.Name] = command;
        }

        return commands;
    }

    internal void ToggleListening()
    {
        throw new NotImplementedException();
    }
}
