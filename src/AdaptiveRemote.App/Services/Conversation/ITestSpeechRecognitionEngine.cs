using PolyType;
using StreamJsonRpc;

namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// Interface for test-only control of speech recognition.
/// This interface is exported via JSON-RPC for tests to control speech recognition.
/// </summary>
[RpcMarshalable]
[JsonRpcContract]
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface ITestSpeechRecognitionEngine
{
    /// <summary>
    /// Raises a SpeechRecognized event with the specified text and semantics.
    /// </summary>
    /// <param name="text">The recognized text.</param>
    /// <param name="confidence">Confidence level (0-100).</param>
    /// <param name="semantics">Optional semantic key-value pairs.</param>
    Task RaiseRecognizedAsync(string text, int confidence, Dictionary<string, string>? semantics = null);

    /// <summary>
    /// Raises a SpeechRejected event with the specified text.
    /// </summary>
    /// <param name="text">The rejected text.</param>
    /// <param name="confidence">Confidence level (0-100).</param>
    Task RaiseRejectedAsync(string text, int confidence);

    /// <summary>
    /// Convenience method to speak a phrase with known semantics.
    /// Recognizes wake words ("Hey Remote"), stop listening ("Thank you"), and standard commands.
    /// </summary>
    /// <param name="phrase">The phrase to speak.</param>
    Task SpeakAsync(string phrase);
}
