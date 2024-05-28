using AdaptiveRemote.Logging;
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
                    _logger.LogInformation(Message.SpeechSynthesis_SelectedVoice, installedVoice);
                    return;
                }
            }
            _logger.LogWarning(Message.SpeechSynthesis_VoiceNotFound, voiceName);
        }
    }

    void ISpeechSynthesis.Say(string phrase)
    {
        _synthesizer.CancelAll();
        _logger.LogInformation(Message.SpeechSynthesis_Saying, phrase);
        _synthesizer.SpeakAsync(phrase);
    }
}
