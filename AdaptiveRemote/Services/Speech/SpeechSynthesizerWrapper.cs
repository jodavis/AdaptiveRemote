using System.Diagnostics.CodeAnalysis;
using System.Speech.Synthesis;

namespace AdaptiveRemote.Services.Speech;

[ExcludeFromCodeCoverage(Justification = "Simple wrapper around System.Speech.SpeechSynthesizer")]
internal class SpeechSynthesizerWrapper : ISpeechSynthesizer, IDisposable
{
    private readonly SpeechSynthesizer _speechSynthesizer = new();

    public void SetOutputToDefaultAudioDevice() => _speechSynthesizer.SetOutputToDefaultAudioDevice();
    public void CancelAll() => _speechSynthesizer.SpeakAsyncCancelAll();
    public void SpeakAsync(string textToSpeak) => _speechSynthesizer.SpeakAsync(textToSpeak);
    public void Dispose() => _speechSynthesizer.Dispose();
    public IEnumerable<string> GetInstalledVoices() => _speechSynthesizer.GetInstalledVoices().Select(x => x.VoiceInfo.Name);
    public void SelectVoice(string name) => _speechSynthesizer.SelectVoice(name);
}
