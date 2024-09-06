using System.Speech.Recognition;

namespace AdaptiveRemote.Services.Conversation;

internal interface IGrammarProvider
{
    Grammar LoadGrammar(PhraseKinds phraseKind);
}
