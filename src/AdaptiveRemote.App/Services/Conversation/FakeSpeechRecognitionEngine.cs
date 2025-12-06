using System.Diagnostics.CodeAnalysis;

namespace AdaptiveRemote.Services.Conversation;

internal class FakeSpeechRecognitionEngine : ISpeechRecognitionEngine
{
    private readonly Dictionary<string, IGrammar> _grammars = new();

    private event EventHandler<RecognizedSpeechEventArgs>? _recognized;
    private TaskCompletionSource _pause = new();

    public FakeSpeechRecognitionEngine()
    {
        _ = RecognitionLoop();
    }

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
    void ISpeechRecognitionEngine.RecognizeAsync() => _pause.TrySetResult();
    void ISpeechRecognitionEngine.RecognizeAsyncCancel() => _pause = new();
    void ISpeechRecognitionEngine.SetConfidenceThreshold(int threshold) { }

    private async Task RecognitionLoop()
    {
        IEnumerator<FakeRecognitionResult> commands = CommandLoop();
        int ticks = 0;

        while (true)
        {
            await _pause.Task;
            await Task.Delay(1000);
            ticks++;

            if (IsEnabled(PhraseKinds.WakeWord))
            {
                if (ticks >= 5)
                {
                    ticks = 0;
                    SendResult(new("Hey Remote", 80, ("system", "STARTLISTENING")));
                }
            }
            else if (IsEnabled(PhraseKinds.Commands))
            {
                if (ticks >= 3)
                {
                    ticks = 0;
                    commands.MoveNext();
                    SendResult(commands.Current);
                }
            }
            else
            {
                ticks = 0;
            }
        }
    }

    private static IEnumerator<FakeRecognitionResult> CommandLoop()
    {
        while (true)
        {
            yield return Command("Go up", "Up");
            yield return Command("Down three", "Down", repeat: 3);
            yield return new FakeRecognitionResult("That's wrong", 80, ("correction", "true"));
            yield return Command("TiVo", "TiVo");
            yield return Command("Louder 5 times", "VolumeUp", repeat: 5);
            yield return Command("Mute", "Mute", confidence: 40);
            yield return new FakeRecognitionResult("Yes, I did", 80, ("confirmation", "YES"));
            yield return Command("Volume Down 5", "VolumeDown", repeat: 5);
            yield return Command("Guide", "Guide", confidence: 40);
            yield return new FakeRecognitionResult("No", 80, ("confirmation", "NO"));
            yield return Command("Go up", "Up");
            yield return new FakeRecognitionResult("That's wrong", 80, ("correction", "true"));
            yield return Command("Back", "Back");
            yield return new FakeRecognitionResult("Thank you", 80, ("system", "STOPLISTENING"), ("thankyou", "true"));
        }

        static FakeRecognitionResult Command(string text, string command, int confidence = 80, int? repeat = default)
            => repeat is null
                ? new FakeRecognitionResult(text, confidence, (nameof(command), command))
                : new FakeRecognitionResult(text, confidence, (nameof(command), command), (nameof(repeat), repeat.ToString()!));
    }

    private bool IsEnabled(PhraseKinds phraseKind)
        => _grammars.TryGetValue(phraseKind.ToString(), out IGrammar? grammar) && grammar.Enabled;

    private void SendResult(FakeRecognitionResult result)
    {
        _recognized?.Invoke(this, new(result));
    }

    private class FakeRecognitionResult : IRecognizedSpeech
    {
        internal FakeRecognitionResult(string text, int confidence, params (string, string)[] semantics)
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
