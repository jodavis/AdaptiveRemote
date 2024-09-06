using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Conversation;

internal class ConversationStateMachine
{
    private static readonly IReadOnlyDictionary<string, Command> EmptyCommands = new Dictionary<string, Command>();

    private ConversationState _state;
    private readonly IRemoteDefinitionService _definitionService;

    public ConversationStateMachine(IRemoteDefinitionService definitionService, ILogger<ConversationStateMachine> logger)
    {
        _definitionService = definitionService;
        Logger = logger;

        _state = new(EmptyCommands);
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
        _state = _state.ToggleListening(Logger);
    }

    internal void Reset()
    {
        _state = new(GetCommands(_definitionService), WantsPhrases: PhraseKinds.WakeWord);
    }
}
