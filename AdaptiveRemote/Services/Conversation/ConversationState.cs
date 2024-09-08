using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Conversation;

internal record ConversationState(
    IReadOnlyDictionary<string, Command> Commands,
    int HighConfidenceThreshold,
    IRecognizedSpeech? LastSpeech = default,
    IRecognizedSpeech? LastCommand = default,
    ConversationResponse? CurrentResponse = default,
    ConversationResponse? LastResponseWithCommands = default,
    PhraseKinds WantsPhrases = PhraseKinds.None);
