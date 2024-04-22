namespace AdaptiveRemote.Services.Speech;

public interface ISpeechSynthesizer
{
    void SetOutputToDefaultAudioDevice();

    void SpeakAsync(string phrase);

    void CancelAll();

    bool HasVoice(string name);

    void SelectVoice(string voice);
}
