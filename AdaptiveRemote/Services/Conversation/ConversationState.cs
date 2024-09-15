using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Conversation;

internal record ConversationState(
    IReadOnlyDictionary<string, Command> Commands,
    IRecognizedSpeech? LastCommand = default,
    ConversationResponse? LastResponse = default,
    PhraseKinds WantsPhrases = PhraseKinds.None);
