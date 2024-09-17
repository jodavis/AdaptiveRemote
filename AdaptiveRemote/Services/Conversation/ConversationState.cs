using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Conversation;

internal record ConversationState(
    IReadOnlyDictionary<string, Command> Commands,
    int HighConfidenceThreshold,
    IRecognizedSpeech? SpeechToConfirm = default,
    ConversationResponse? CurrentResponse = default,
    ConversationResponse? ResponseToCorrect = default,
    PhraseKinds WantsPhrases = PhraseKinds.None);
