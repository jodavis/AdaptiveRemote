using System.Diagnostics.CodeAnalysis;

namespace AdaptiveRemote.Services.Speech;

public interface ISpeechSynthesizer
{
    void SetOutputToDefaultAudioDevice();

    void SpeakAsync(string phrase);

    void CancelAll();

    IEnumerable<string> GetInstalledVoices();

    void SelectVoice(string fullName);
}
