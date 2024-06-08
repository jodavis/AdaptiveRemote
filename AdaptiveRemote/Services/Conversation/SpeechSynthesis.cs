using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Conversation;
internal class SpeechSynthesis : ISpeechSynthesis
{
    private readonly ISpeechSynthesizer _synthesizer;
    private readonly IListeningController _listeningController;
    private readonly ILogger<SpeechSynthesis> _logger;

    private TaskCompletionSource _tcs = new();

    public SpeechSynthesis(ISpeechSynthesizer synthesizer, IListeningController listeningController, IOptionsSnapshot<ConversationSettings> settings, ILogger<SpeechSynthesis> logger)
    {
        _synthesizer = synthesizer;
        _listeningController = listeningController;
        _logger = logger;

        SelectVoice(settings.Value.Voice);
        SetSpeakingRate(settings.Value.SpeakingRate);

        _synthesizer.SpeakCompleted += OnSpeakCompleted;
        _tcs.SetResult();
    }

    private void OnSpeakCompleted(object? sender, EventArgs e) => _tcs.TrySetResult();

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

    private void SetSpeakingRate(int speakingRate)
    {
        _synthesizer.SetSpeakingRate(speakingRate);

    }

    async Task ISpeechSynthesis.SayAsync(string phrase, CancellationToken cancellationToken)
    {
        if (!_tcs.Task.IsCompleted)
        {
            _logger.LogAndThrowError(Message.SpeechSynthesis_AlreadySpeaking, phrase);
        }

        _tcs = new();

        using (cancellationToken.Register(() => CancelSpeaking(_tcs, phrase)))
        using (_listeningController.Pause())
        {
            _logger.LogInformation(Message.SpeechSynthesis_Saying, phrase);
            _synthesizer.SpeakAsync(phrase);
            await _tcs.Task;
        }
    }

    private void CancelSpeaking(TaskCompletionSource tcs, string phrase)
    {
        _logger.LogInformation(Message.SpeechSynthesis_CancelledSaying, phrase);
        tcs.TrySetCanceled();
        _synthesizer.CancelAll();
    }
}
