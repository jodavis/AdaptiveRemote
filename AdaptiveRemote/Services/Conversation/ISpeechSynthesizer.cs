namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// A thin wrapper on System.Speech.Synthesis.SpeechSynthesizer
/// </summary>
public interface ISpeechSynthesizer
{
    /// <summary>
    /// Generates speech output asynchronously from a string.
    /// </summary>
    void SpeakAsync(string phrase);

    /// <summary>
    /// Cancels all queued, asynchronous, speech synthesis operations.
    /// </summary>
    void CancelAll();

    /// <summary>
    /// Returns the collection of speech synthesis (text-to-speech) voices that 
    /// are currently installed on the system.
    /// </summary>
    IEnumerable<string> GetInstalledVoices();

    /// <summary>
    /// Selects a specific voice by name.
    /// </summary>
    void SelectVoice(string fullName);

    /// <summary>
    /// Sets the speaking rate of the SpeechSynthesizer object.
    /// </summary>
    void SetSpeakingRate(int rate);

    /// <summary>
    /// Raised when the SpeechSynthesizer completes the speaking of a prompt.
    /// </summary>
    event EventHandler SpeakCompleted;
}
