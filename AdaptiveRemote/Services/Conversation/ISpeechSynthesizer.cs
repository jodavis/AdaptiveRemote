using System.Diagnostics.CodeAnalysis;

namespace AdaptiveRemote.Services.Conversation;

public interface ISpeechSynthesizer
{
    void SpeakAsync(string phrase);

    void CancelAll();

    IEnumerable<string> GetInstalledVoices();

    void SelectVoice(string fullName);

    void SetSpeakingRate(int rate);

    event EventHandler SpeakCompleted;
}
