namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// Abstracts System.Speech.Recognition.Grammar to allow cross-platform implementations.
/// </summary>
public interface IGrammar
{
    /// <summary>
    /// Gets the name associated with this grammar.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this grammar should be used in speech
    /// recognition.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    /// When the engine needs the underlying System.Speech.Recognition.Grammar instance
    /// (only available on Windows host), this method returns it. Implementations on
    /// non-Windows hosts may return null.
    /// </summary>
    object? GetNativeGrammar();
}
