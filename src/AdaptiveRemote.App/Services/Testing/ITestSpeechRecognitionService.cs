using PolyType;
using StreamJsonRpc;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Interface for controlling speech recognition in tests.
/// Allows tests to simulate speech input programmatically.
/// </summary>
[RpcMarshalable]
[JsonRpcContract]
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface ITestSpeechRecognitionService : IDisposable
{
    /// <summary>
    /// Simulates speaking a phrase that should be recognized by the speech recognition system.
    /// </summary>
    /// <param name="text">The text that was "spoken".</param>
    /// <param name="confidence">Confidence level (0-100), defaults to 80.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task that completes when the speech has been processed.</returns>
    Task SpeakPhraseAsync(string text, int confidence, CancellationToken cancellationToken);
}
