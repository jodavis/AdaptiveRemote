using AdaptiveRemote.Services.Conversation;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Test implementation of ISpeechSynthesis that records spoken phrases instead of actually speaking.
/// </summary>
internal class TestSpeechSynthesis : ISpeechSynthesis
{
    private const int SpeechDelayMs = 1000; // Delay to simulate speech duration for testing

    private readonly List<string> _spokenPhrases = new();
    private readonly object _lock = new();

    Task ISpeechSynthesis.SayAsync(string phrase, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _spokenPhrases.Add(phrase);
        }

        // Add a delay to simulate speech duration so the modal remains visible during tests.
        // This matches the behavior of StubSpeechSynthesizer used in the headless host.
        return Task.Delay(SpeechDelayMs, cancellationToken);
    }

    public string[] GetSpokenPhrases()
    {
        lock (_lock)
        {
            return _spokenPhrases.ToArray();
        }
    }

    public void ClearSpokenPhrases()
    {
        lock (_lock)
        {
            _spokenPhrases.Clear();
        }
    }
}
