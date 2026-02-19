using System.Diagnostics.CodeAnalysis;
using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Testing;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Test implementation of ISpeechRecognitionEngine that allows tests to programmatically
/// trigger speech recognition events.
/// </summary>
public class TestSpeechRecognitionEngine : ISpeechRecognitionEngine
{
    private readonly Dictionary<string, IGrammar> _grammars = new();
    private event EventHandler<RecognizedSpeechEventArgs>? _recognized;
    private event EventHandler<RecognizedSpeechEventArgs>? _rejected;

    event EventHandler<RecognizedSpeechEventArgs> ISpeechRecognitionEngine.SpeechRecognized
    {
        add => _recognized += value;
        remove => _recognized -= value;
    }

    event EventHandler<RecognizedSpeechEventArgs> ISpeechRecognitionEngine.SpeechRejected
    {
        add => _rejected += value;
        remove => _rejected -= value;
    }

    void ISpeechRecognitionEngine.LoadGrammar(IGrammar grammar)
        => _grammars[grammar.Name ?? string.Empty] = grammar;

    void ISpeechRecognitionEngine.UnloadGrammar(IGrammar grammar)
        => _grammars.Remove(grammar.Name ?? string.Empty);

    void ISpeechRecognitionEngine.UnloadAllGrammars()
        => _grammars.Clear();

    void ISpeechRecognitionEngine.Recognize()
    {
        // In test mode, recognition is controlled by test calls, not automatic
        // Verify that at least one grammar is loaded and enabled
        if (!_grammars.Values.Any(g => g.Enabled))
        {
            throw new InvalidOperationException(
                "Cannot start recognition: No enabled grammars are loaded. " +
                "Load and enable a grammar before calling Recognize().");
        }
    }

    void ISpeechRecognitionEngine.RecognizeAsyncCancel()
    {
        // In test mode, there's no async recognition to cancel
    }

    void ISpeechRecognitionEngine.SetConfidenceThreshold(int threshold)
    {
        // Test engine doesn't filter by confidence
    }

    public Task RaiseRecognizedAsync(string text, int confidence, Dictionary<string, string>? semantics = null)
    {
        TestRecognitionResult result = new(text, confidence, semantics ?? new Dictionary<string, string>());
        _recognized?.Invoke(this, new RecognizedSpeechEventArgs(result));
        return Task.CompletedTask;
    }

    public Task RaiseRejectedAsync(string text, int confidence)
    {
        TestRecognitionResult result = new(text, confidence, new Dictionary<string, string>());
        _rejected?.Invoke(this, new RecognizedSpeechEventArgs(result));
        return Task.CompletedTask;
    }

    private class TestRecognitionResult : IRecognizedSpeech
    {
        private readonly Dictionary<string, string> _semantics;

        internal TestRecognitionResult(string text, int confidence, Dictionary<string, string> semantics)
        {
            Text = text;
            Confidence = confidence;
            _semantics = semantics;
        }

        public string Text { get; }
        public int Confidence { get; }

        bool IRecognizedSpeech.ContainsSemanticValue(string key)
            => _semantics.ContainsKey(key);

        bool IRecognizedSpeech.TryGetSemanticValue(string key, [NotNullWhen(true)] out string? value)
            => _semantics.TryGetValue(key, out value);

        void IRecognizedSpeech.WriteToWaveStream(Stream waveStream)
            => throw new NotSupportedException("Test recognition results do not have audio data");
    }
}
