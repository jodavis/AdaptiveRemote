using System.Speech.Recognition;
using System.Speech.Synthesis;

namespace AdaptiveRemote.Services.Conversation;

internal interface IAudioConfigurationService
{
    void Configure(SpeechRecognitionEngine recognition);

    void Configure(SpeechSynthesizer synthesizer);
}
