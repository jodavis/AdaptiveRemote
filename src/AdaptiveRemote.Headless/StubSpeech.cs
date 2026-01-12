using AdaptiveRemote.Services.Conversation;

namespace AdaptiveRemote.Headless;

// AdaptiveRemote depends on Windows Speech Services, so we don't have
// a speech system that works cross-platform yet.

internal class StubSpeechSynthesizer : ISpeechSynthesizer
{
    public event EventHandler? SpeakCompleted;

    public void CancelAll() { }
    public void Speak(string phrase) { SpeakCompleted?.Invoke(this, EventArgs.Empty); }
    public IEnumerable<string> GetInstalledVoices() => Array.Empty<string>();
    public void SelectVoice(string fullName) { }
    public void SetSpeakingRate(int rate) { }
}

internal class StubSpeechRecognition : ISpeechRecognitionEngine
{
    // Events are not used in headless mode - no speech recognition needed
    public event EventHandler<RecognizedSpeechEventArgs> SpeechRecognized { add { } remove { } }
    public event EventHandler<RecognizedSpeechEventArgs> SpeechRejected { add { } remove { } }

    public void LoadGrammar(IGrammar grammar) { }
    public void Recognize() { }
    public void RecognizeAsyncCancel() { }
    public void SetConfidenceThreshold(int threshold) { }
    public void UnloadAllGrammars() { }
    public void UnloadGrammar(IGrammar grammar) { }
}

internal class StubGrammarProvider : IGrammarProvider
{
    public IGrammar LoadGrammar(PhraseKinds phraseKind) => new StubGrammar(phraseKind.ToString());

    private record class StubGrammar(string Name) : IGrammar
    {
        public bool Enabled { get; set; }
        public object? GetNativeGrammar() => null;
    }
}
