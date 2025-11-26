using System.Speech.Recognition;
using System.Speech.Synthesis;

namespace AdaptiveRemote.Services.Conversation;

internal class DefaultDeviceAudioConfiguration : IAudioConfigurationService
{
    public void Configure(SpeechRecognitionEngine recognition)
        => recognition.SetInputToDefaultAudioDevice();

    public void Configure(SpeechSynthesizer synthesizer)
        => synthesizer.SetOutputToDefaultAudioDevice();
}
