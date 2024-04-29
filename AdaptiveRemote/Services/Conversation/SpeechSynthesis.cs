using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Conversation;
internal class SpeechSynthesis : ISpeechSynthesis
{
    private readonly ISpeechSynthesizer _synthesizer;
    private readonly ILogger<SpeechSynthesis> _logger;

    public SpeechSynthesis(ISpeechSynthesizer synthesizer, IOptionsSnapshot<ConversationSettings> settings, ILogger<SpeechSynthesis> logger)
    {
        _synthesizer = synthesizer;
        _logger = logger;

        SelectVoice(settings.Value.Voice);

        _synthesizer.SetOutputToDefaultAudioDevice();
    }

    private void SelectVoice(string[] voiceNames)
    {
        foreach (string voiceName in voiceNames)
        {
            foreach (string installedVoice in _synthesizer.GetInstalledVoices())
            {
                if (installedVoice.Contains(voiceName, StringComparison.OrdinalIgnoreCase))
                {
                    _synthesizer.SelectVoice(installedVoice);
                    _logger.LogInformation(LoggingMessages.SpeechSynthesis_SelectedVoice, installedVoice);
                    return;
                }
            }
            _logger.LogWarning(LoggingMessages.SpeechSynthesis_VoiceNotFound, voiceName);
        }
    }

    void ISpeechSynthesis.Say(string phrase)
    {
        _synthesizer.CancelAll();
        _logger.LogInformation(LoggingMessages.SpeechSynthesis_Saying, phrase);
        _synthesizer.SpeakAsync(phrase);
    }
}
