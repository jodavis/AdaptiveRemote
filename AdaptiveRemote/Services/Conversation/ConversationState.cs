using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Conversation;

internal record ConversationState(
    IReadOnlyDictionary<string, Command> Commands,
    int HighConfidenceThreshold,
    IRecognizedSpeech? LastCommand = default,
    ConversationResponse? LastResponse = default,
    PhraseKinds WantsPhrases = PhraseKinds.None);
