using System.Speech.Recognition;

namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// A wrapper around System.Speech.Recognition.Grammar that implements IGrammar
/// </summary>
internal class GrammarWrapper : IGrammar
{
    internal GrammarWrapper(Grammar grammar)
    {
        Grammar = grammar;
    }

    /// <summary>
    /// Gets the underlying System.Speech.Recognition.Grammar
    /// </summary>
    internal Grammar Grammar { get; }

    public string Name => Grammar.Name;

    public bool Enabled
    {
        get => Grammar.Enabled;
        set => Grammar.Enabled = value;
    }
}
