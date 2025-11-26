namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// A fake grammar provider for testing purposes that creates FakeGrammar instances
/// </summary>
internal class FakeGrammarProvider : IGrammarProvider
{
    IGrammar IGrammarProvider.LoadGrammar(PhraseKinds phraseKind)
        => new FakeGrammar(phraseKind.ToString());
}
