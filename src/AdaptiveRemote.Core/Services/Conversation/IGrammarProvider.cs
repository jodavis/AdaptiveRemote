namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// Loader for Grammar objects
/// </summary>
public interface IGrammarProvider
{
    /// <summary>
    /// Load a grammar that supports the given kind of phrases
    /// </summary>
    IGrammar LoadGrammar(PhraseKinds phraseKind);
}
