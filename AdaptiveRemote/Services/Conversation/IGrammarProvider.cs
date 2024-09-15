using System.Speech.Recognition;

namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// Loader for Grammar objects
/// </summary>
internal interface IGrammarProvider
{
    /// <summary>
    /// Load a grammar that supports the given kind of phrases
    /// </summary>
    Grammar LoadGrammar(PhraseKinds phraseKind);
}
