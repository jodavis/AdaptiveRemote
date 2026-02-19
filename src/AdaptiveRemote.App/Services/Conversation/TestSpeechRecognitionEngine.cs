using System.Diagnostics.CodeAnalysis;

namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// Test implementation of ISpeechRecognitionEngine that allows tests to programmatically
/// trigger speech recognition events.
/// </summary>
public class TestSpeechRecognitionEngine : ISpeechRecognitionEngine, ITestSpeechRecognitionEngine
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

    public Task SpeakAsync(string phrase)
    {
        // Map common phrases to their semantic meanings
        Dictionary<string, string> semantics = phrase.ToLowerInvariant() switch
        {
            "hey remote" => new() { ["system"] = "STARTLISTENING" },
            "thank you" => new()
            {
                ["system"] = "STOPLISTENING",
                ["thankyou"] = "true"
            },
            _ => ParseCommand(phrase)
        };

        return RaiseRecognizedAsync(phrase, 80, semantics);
    }

    private static Dictionary<string, string> ParseCommand(string phrase)
    {
        // Simple command parsing for common commands
        Dictionary<string, string> semantics = new();
        string lowerPhrase = phrase.ToLowerInvariant();

        // Check for repeat patterns like "up 3 times" or "down 5"
        string[] words = lowerPhrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Look for command words
        string? command = ExtractCommand(words);
        if (command is not null)
        {
            semantics["command"] = command;

            // Look for repeat count
            int? repeat = ExtractRepeatCount(words);
            if (repeat.HasValue)
            {
                semantics["repeat"] = repeat.Value.ToString();
            }
        }

        return semantics;
    }

    private static string? ExtractCommand(string[] words)
    {
        // Map of recognized commands
        Dictionary<string, string> commandMap = new()
        {
            ["up"] = "Up",
            ["down"] = "Down",
            ["left"] = "Left",
            ["right"] = "Right",
            ["select"] = "Select",
            ["ok"] = "Select",
            ["back"] = "Back",
            ["play"] = "Play",
            ["pause"] = "Pause",
            ["stop"] = "Stop",
            ["record"] = "Record",
            ["guide"] = "Guide",
            ["tivo"] = "TiVo",
            ["mute"] = "Mute",
            ["louder"] = "VolumeUp",
            ["quieter"] = "VolumeDown",
            ["volume"] = ExtractVolumeCommand(words) ?? string.Empty,
            ["channel"] = ExtractChannelCommand(words) ?? string.Empty,
        };

        foreach (string word in words)
        {
            if (commandMap.TryGetValue(word, out string? command) && command is not null)
            {
                return command;
            }
        }

        return null;
    }

    private static string? ExtractVolumeCommand(string[] words)
    {
        // Look for "volume up" or "volume down"
        for (int i = 0; i < words.Length - 1; i++)
        {
            if (words[i] == "volume")
            {
                if (words[i + 1] == "up")
                {
                    return "VolumeUp";
                }

                if (words[i + 1] == "down")
                {
                    return "VolumeDown";
                }
            }
        }
        return null;
    }

    private static string? ExtractChannelCommand(string[] words)
    {
        // Look for "channel up" or "channel down"
        for (int i = 0; i < words.Length - 1; i++)
        {
            if (words[i] == "channel")
            {
                if (words[i + 1] == "up")
                {
                    return "ChannelUp";
                }

                if (words[i + 1] == "down")
                {
                    return "ChannelDown";
                }
            }
        }
        return null;
    }

    private static int? ExtractRepeatCount(string[] words)
    {
        // Look for numbers followed by "times" or just standalone numbers
        for (int i = 0; i < words.Length; i++)
        {
            if (int.TryParse(words[i], out int count))
            {
                return count;
            }
        }
        return null;
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

    public void Dispose()
    {
        // No resources to dispose
        GC.SuppressFinalize(this);
    }
}
