using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Testing;

namespace AdaptiveRemote.EndtoEndTests;

internal class SpeechTestService : ISpeechTestService
{
    private readonly TestSpeechRecognitionEngine _speechRecognitionEngine;
    private readonly TestSpeechSynthesis _speechSynthesis;

    public SpeechTestService(ISpeechRecognitionEngine speechRecognitionEngine, ISpeechSynthesis speechSynthesis)
    {
        _speechRecognitionEngine = speechRecognitionEngine as TestSpeechRecognitionEngine
            ?? throw new ArgumentException("TestSpeechRecognitionEngine was not registered with the host service provider", nameof(speechRecognitionEngine));
        _speechSynthesis = speechSynthesis as TestSpeechSynthesis
            ?? throw new ArgumentException("TestSpeechSynthesis was not registered with the host service provider", nameof(speechSynthesis));
    }

    void IDisposable.Dispose()
    {
        // No resources to dispose
    }

    Task ISpeechTestService.RaiseRecognizedAsync(string text, int confidence, Dictionary<string, string>? semantics, CancellationToken cancellationToken)
        => _speechRecognitionEngine.RaiseRecognizedAsync(text, confidence, semantics);

    Task ISpeechTestService.RaiseRejectedAsync(string text, int confidence, CancellationToken cancellationToken)
        => _speechRecognitionEngine.RaiseRejectedAsync(text, confidence);

    Task<string[]> ISpeechTestService.GetSpokenPhrasesAsync(CancellationToken cancellationToken)
        => Task.FromResult(_speechSynthesis.GetSpokenPhrases());

    Task ISpeechTestService.ClearSpokenPhrasesAsync(CancellationToken cancellationToken)
    {
        _speechSynthesis.ClearSpokenPhrases();
        return Task.CompletedTask;
    }
}
