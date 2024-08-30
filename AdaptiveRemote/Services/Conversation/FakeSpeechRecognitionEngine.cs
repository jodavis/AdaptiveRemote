using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Speech.Recognition;

namespace AdaptiveRemote.Services.Conversation;

internal class FakeSpeechRecognitionEngine : ISpeechRecognitionEngine
{
    private readonly Dictionary<string, Grammar> _grammars = new();

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

    event EventHandler<RecognitionErrorEventArgs> ISpeechRecognitionEngine.RecognitionError
    {
        add { }
        remove { }
    }

    void ISpeechRecognitionEngine.LoadGrammar(Grammar grammar) => _grammars.Add(grammar.Name, grammar);
    void ISpeechRecognitionEngine.UnloadGrammar(Grammar grammar) => _grammars.Remove(grammar.Name);
    void ISpeechRecognitionEngine.UnloadAllGrammars() => _grammars.Clear();
    void ISpeechRecognitionEngine.RecognizeAsync() => _pause.TrySetResult();
    void ISpeechRecognitionEngine.RecognizeAsyncCancel() => _pause = new();
    void ISpeechRecognitionEngine.UpdateRecognizerSetting(string name, int value) { }

    private async Task RecognitionLoop()
    {
        IEnumerator<FakeRecognitionResult> commands = CommandLoop();
        int ticks = 0;

        while (true)
        {
            await _pause.Task;
            await Task.Delay(1000);
            ticks++;

            if (IsEnabled("Attention"))
            {
                if (ticks >= 5)
                {
                    ticks = 0;
                    SendResult(new("Hey TiVo", ("system", "STARTLISTENING")));
                }
            }
            else if (IsEnabled("Commands"))
            {
                if (ticks >= 2)
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
            yield return Command("Down three", "Down", 3);
            yield return Command("TiVo", "TiVo");
            yield return Command("Louder 5 times", "VolumeUp", 5);
            yield return Command("Mute", "Mute");
            yield return Command("Volume Down 5", "VolumeDown", 5);
            yield return Command("Guide", "Guide");
            yield return Command("Go up", "Up");
            yield return Command("Down three", "Down", 3);
            yield return Command("Back", "Back");
            yield return new FakeRecognitionResult("Thank you", ("system", "STOPLISTENING"), ("thankyou", "true"));
        }

        static FakeRecognitionResult Command(string text, string command, int? repeat = default)
            => repeat is null
                ? new FakeRecognitionResult(text, (nameof(command), command))
                : new FakeRecognitionResult(text, (nameof(command), command), (nameof(repeat), repeat.ToString()!));
    }

    private bool IsEnabled(string grammarName)
        => _grammars.TryGetValue(grammarName, out Grammar? grammar) && grammar.Enabled;

    private void SendResult(FakeRecognitionResult result)
    {
        _recognized?.Invoke(this, new(result));
    }

    private class FakeRecognitionResult : IRecognizedSpeech
    {
        internal FakeRecognitionResult(string text, params (string, string)[] semantics)
        {
            Text = text;
            _semantics = semantics;
        }

        public string Text { get; }

        private readonly (string, string)[] _semantics;

        bool IRecognizedSpeech.ContainsSemanticValue(string key)
            => _semantics.Any(x => x.Item1 == key);
        bool IRecognizedSpeech.TryGetSemanticValue(string key, [NotNullWhen(true)] out string? value)
            => (value = _semantics.Where(x => x.Item1 == key).Select(x => x.Item2).FirstOrDefault()) is not null;

        void IRecognizedSpeech.WriteToWaveStream(Stream waveStream) => throw new NotImplementedException();
    }
}
