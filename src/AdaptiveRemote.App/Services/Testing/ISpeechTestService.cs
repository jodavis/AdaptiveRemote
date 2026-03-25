using PolyType;
using StreamJsonRpc;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Interface for test-only control of speech recognition.
/// This interface is exported via JSON-RPC for tests to control speech recognition.
/// </summary>
[RpcMarshalable]
[JsonRpcContract]
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface ISpeechTestService : IDisposable
{
    /// <summary>
    /// Raises a SpeechRecognized event with the specified text and semantics.
    /// </summary>
    /// <param name="text">The recognized text.</param>
    /// <param name="confidence">Confidence level (0-100).</param>
    /// <param name="semantics">Optional semantic key-value pairs.</param>
    Task RaiseRecognizedAsync(string text, int confidence, Dictionary<string, string>? semantics = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises a SpeechRejected event with the specified text.
    /// </summary>
    /// <param name="text">The rejected text.</param>
    /// <param name="confidence">Confidence level (0-100).</param>
    Task RaiseRejectedAsync(string text, int confidence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the phrases that have been spoken by the test speech synthesis service.
    /// </summary>
    Task<string[]> GetSpokenPhrasesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the spoken phrases tracked by the test speech synthesis service.
    /// </summary>
    Task ClearSpokenPhrasesAsync(CancellationToken cancellationToken = default);
}
