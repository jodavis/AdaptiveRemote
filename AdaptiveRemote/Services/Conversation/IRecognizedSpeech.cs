using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace AdaptiveRemote.Services.Conversation;

public interface IRecognizedSpeech
{
    /// <summary>
    /// The text representation of the speech that was recognized
    /// </summary>
    string Text { get; }

    /// <summary>
    /// Check whether a semantic value was provided by the grammar for the recognized phrase
    /// </summary>
    /// <param name="key">The key for the semantic value</param>
    bool ContainsSemanticValue(string key);

    /// <summary>
    /// Get a semantic value provided by the grammar for the recognized phrase
    /// </summary>
    /// <param name="key">The key for the semantic value</param>
    /// <param name="value">The semantic value</param>
    /// <returns>True if the given key exists in the semantic representation, otherwise false</returns>
    bool TryGetSemanticValue(string key, [NotNullWhen(true)] out string? value);

    /// <summary>
    /// Collect an audio .wav file of the speech that was recognized, for diagnostic purposes
    /// </summary>
    /// <param name="waveStream">The stream to write the .wav file to</param>
    void WriteToWaveStream(Stream waveStream);
}
