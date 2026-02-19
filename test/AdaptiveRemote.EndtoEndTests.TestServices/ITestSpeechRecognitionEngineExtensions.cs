using AdaptiveRemote.Services.Testing;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Extension methods for ITestSpeechRecognitionEngine.
/// </summary>
public static class ITestSpeechRecognitionEngineExtensions
{
    /// <summary>
    /// Convenience method to speak a phrase with known semantics.
    /// Recognizes wake words ("Hey Remote"), stop listening ("Thank you"), and standard commands.
    /// This runs in the test process and only calls into the host to raise events.
    /// </summary>
    /// <param name="engine">The test speech recognition engine.</param>
    /// <param name="phrase">The phrase to speak.</param>
    public static Task SpeakAsync(this ITestSpeechRecognitionEngine engine, string phrase)
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

        return engine.RaiseRecognizedAsync(phrase, 80, semantics);
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
}
