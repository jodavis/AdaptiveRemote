using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Conversation;

internal class ConversationStateMachine
{
    private static readonly IReadOnlyDictionary<string, Command> EmptyCommands = new Dictionary<string, Command>();

    private ConversationState _state;
    private readonly ConversationSettings _settings;
    private readonly IRemoteDefinitionService _definitionService;

    public ConversationStateMachine(IRemoteDefinitionService definitionService, IOptions<ConversationSettings> settings, ILogger<ConversationStateMachine> logger)
    {
        _definitionService = definitionService;
        Logger = logger;

        _state = new(EmptyCommands, 101);
        _settings = settings.Value;
    }

    public ILogger Logger { get; }
    public PhraseKinds WantPhrases => _state.WantsPhrases;

    internal ConversationResponse RespondTo(IRecognizedSpeech result)
    {
        return (_state = _state.RespondTo(result, Logger)).CurrentResponse
            ?? throw new Exception("State machine did not produce a ConversationResponse");
    }

    internal void ToggleListening()
    {
        _state = _state.ToggleListening(Logger);
    }

    internal void Reset()
    {
        IReadOnlyDictionary<string, Command> commands = _definitionService.GetCommands().ToDictionary(x => x.Name);
        _state = new(commands, HighConfidenceThreshold: _settings.HighConfidenceThreshold, WantsPhrases: PhraseKinds.WakeWord);
    }
}
