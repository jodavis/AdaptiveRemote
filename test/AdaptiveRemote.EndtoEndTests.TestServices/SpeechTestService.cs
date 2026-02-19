using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Testing;

namespace AdaptiveRemote.EndtoEndTests;

internal class SpeechTestService : ISpeechTestService
{
    private readonly TestSpeechRecognitionEngine _speechRecognitionEngine;

    public SpeechTestService(ISpeechRecognitionEngine speechRecognitionEngine)
    {
        _speechRecognitionEngine = speechRecognitionEngine as TestSpeechRecognitionEngine
            ?? throw new ArgumentException("TestSpeechRecognitionEngine was not registered with the host service provider", nameof(speechRecognitionEngine));
    }

    void IDisposable.Dispose()
    {
        // No resources to dispose
    }

    Task ISpeechTestService.RaiseRecognizedAsync(string text, int confidence, Dictionary<string, string>? semantics, CancellationToken cancellationToken)
        => _speechRecognitionEngine.RaiseRecognizedAsync(text, confidence, semantics);

    Task ISpeechTestService.RaiseRejectedAsync(string text, int confidence, CancellationToken cancellationToken)
        => _speechRecognitionEngine.RaiseRejectedAsync(text, confidence);
}
