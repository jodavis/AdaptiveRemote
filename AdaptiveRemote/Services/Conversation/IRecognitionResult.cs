using System.Diagnostics.CodeAnalysis;

namespace AdaptiveRemote.Services.Conversation;

public interface IRecognitionResult
{
    string Text { get; }
    string SemanticMeaning { get; }

    bool ContainsSemanticValue(string key);
    bool TryGetSemanticValue(string key, [NotNullWhen(true)] out string? value);
}
