namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// Abstracts System.Speech.Recognition.Grammar to allow cross-platform implementations.
/// </summary>
public interface IGrammar
{
    string? Name { get; }

    bool Enabled { get; set; }

    /// <summary>
    /// When the engine needs the underlying System.Speech.Recognition.Grammar instance
    /// (only available on Windows host), this method returns it. Implementations on
    /// non-Windows hosts may return null.
    /// </summary>
    object? GetNativeGrammar();
}
