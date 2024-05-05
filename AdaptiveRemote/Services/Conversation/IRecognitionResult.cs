using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace AdaptiveRemote.Services.Conversation;

public interface IRecognitionResult
{
    string Text { get; }
    string SemanticMeaning { get; }

    bool ContainsSemanticValue(string key);
    bool TryGetSemanticValue(string key, [NotNullWhen(true)] out string? value);
    void WriteToWaveStream(Stream waveStream);
}
