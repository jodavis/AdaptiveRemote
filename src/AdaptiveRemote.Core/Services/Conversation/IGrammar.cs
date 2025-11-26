namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// Platform-independent grammar interface that abstracts System.Speech.Recognition.Grammar
/// </summary>
internal interface IGrammar
{
    /// <summary>
    /// Gets or sets the name of the grammar.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets or sets whether the grammar is enabled for recognition.
    /// </summary>
    bool Enabled { get; set; }
}
