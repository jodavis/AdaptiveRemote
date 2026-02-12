using AdaptiveRemote.Services.Conversation;
using System.Diagnostics.CodeAnalysis;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Test-controllable speech recognition engine that allows programmatic speech simulation.
/// Used in E2E tests to simulate speech input without requiring actual speech recognition hardware.
/// </summary>
public class TestSpeechRecognitionEngine : ISpeechRecognitionEngine
{
    private readonly Dictionary<string, IGrammar> _grammars = new();
    private event EventHandler<RecognizedSpeechEventArgs>? _recognized;

    event EventHandler<RecognizedSpeechEventArgs> ISpeechRecognitionEngine.SpeechRecognized
    {
        add => _recognized += value;
        remove => _recognized -= value;
    }

    event EventHandler<RecognizedSpeechEventArgs> ISpeechRecognitionEngine.SpeechRejected
    {
        add { }
        remove { }
    }

    void ISpeechRecognitionEngine.LoadGrammar(IGrammar grammar) => _grammars.Add(grammar.Name ?? string.Empty, grammar);
    void ISpeechRecognitionEngine.UnloadGrammar(IGrammar grammar) => _grammars.Remove(grammar.Name ?? string.Empty);
    void ISpeechRecognitionEngine.UnloadAllGrammars() => _grammars.Clear();
    void ISpeechRecognitionEngine.Recognize() { }
    void ISpeechRecognitionEngine.RecognizeAsyncCancel() { }
    void ISpeechRecognitionEngine.SetConfidenceThreshold(int threshold) { }

    /// <summary>
    /// Simulates speaking a phrase. This is called by the test service to trigger speech recognition.
    /// </summary>
    public void SimulateSpeech(string text, int confidence)
    {
        // Determine the semantics based on the recognized text
        TestRecognitionResult result = text switch
        {
            "Hey Remote" => new(text, confidence, ("system", "STARTLISTENING")),
            "Stop Listening" or "Thank you" => new(text, confidence, ("system", "STOPLISTENING"), ("thankyou", "true")),
            _ => new(text, confidence, ("command", text)) // Generic command
        };

        _recognized?.Invoke(this, new(result));
    }

    private class TestRecognitionResult : IRecognizedSpeech
    {
        internal TestRecognitionResult(string text, int confidence, params (string, string)[] semantics)
        {
            Text = text;
            Confidence = confidence;
            _semantics = semantics;
        }

        public string Text { get; }
        public int Confidence { get; }

        private readonly (string, string)[] _semantics;

        bool IRecognizedSpeech.ContainsSemanticValue(string key)
            => _semantics.Any(x => x.Item1 == key);

        bool IRecognizedSpeech.TryGetSemanticValue(string key, [NotNullWhen(true)] out string? value)
            => (value = _semantics.Where(x => x.Item1 == key).Select(x => x.Item2).FirstOrDefault()) is not null;

        void IRecognizedSpeech.WriteToWaveStream(Stream waveStream) => throw new NotImplementedException();
    }
}
