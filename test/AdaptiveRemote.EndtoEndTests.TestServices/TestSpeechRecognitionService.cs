using AdaptiveRemote.Services.Conversation;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Test service that provides RPC control over speech recognition for E2E tests.
/// </summary>
public class TestSpeechRecognitionService : ITestSpeechRecognitionService
{
    private readonly ISpeechRecognitionEngine _speechEngine;

    public TestSpeechRecognitionService(ISpeechRecognitionEngine speechEngine)
    {
        _speechEngine = speechEngine;
    }

    public Task SpeakPhraseAsync(string text, int confidence, CancellationToken cancellationToken)
    {
        if (_speechEngine is not TestSpeechRecognitionEngine testEngine)
        {
            throw new InvalidOperationException(
                $"Speech recognition engine is not a {nameof(TestSpeechRecognitionEngine)}. " +
                $"Actual type: {_speechEngine.GetType().Name}");
        }

        // Simulate the speech on a background thread to avoid blocking
        Task.Run(() => testEngine.SimulateSpeech(text, confidence), cancellationToken);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Nothing to dispose
        GC.SuppressFinalize(this);
    }
}
