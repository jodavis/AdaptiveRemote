using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Conversation;

internal class SpeechSynthesis : ISpeechSynthesis
{
    private readonly ISpeechSynthesizer _synthesizer;
    private readonly IListeningController _listeningController;
    private readonly ILogger<SpeechSynthesis> _logger;

    private int _isSpeaking = 0;

    public SpeechSynthesis(ISpeechSynthesizer synthesizer, IListeningController listeningController, IOptionsSnapshot<ConversationSettings> settings, ILogger<SpeechSynthesis> logger)
    {
        _synthesizer = synthesizer;
        _listeningController = listeningController;
        _logger = logger;

        SelectVoice(settings.Value.Voice);
        SetSpeakingRate(settings.Value.SpeakingRate);
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

    private void SetSpeakingRate(int speakingRate)
    {
        _synthesizer.SetSpeakingRate(speakingRate);

    }

    async Task ISpeechSynthesis.SayAsync(string phrase, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _isSpeaking, 1) == 1)
        {
            _logger.LogAndThrowError(Message.SpeechSynthesis_AlreadySpeaking, phrase);
        }

        using (_listeningController.Pause())
        {
            try
            {
                _logger.LogInformation(Message.SpeechSynthesis_Saying, phrase);
                await SpeakAndWaitAsync(phrase, cancellationToken);
            }
            finally
            {
                Interlocked.Exchange(ref _isSpeaking, 0);
            }
        }
    }

    private Task SpeakAndWaitAsync(string phrase, CancellationToken cancellationToken)
    {
        TaskCompletionSource tcs = new();

        CancellationTokenRegistration registration = cancellationToken.Register(() => CancelSpeaking(tcs, phrase));

        EventHandler onSpeakCompleted = null!; // This is set on the next line, so it won't be null
        onSpeakCompleted = (sender, e) =>
        {
            _synthesizer.SpeakCompleted -= onSpeakCompleted;
            registration.Dispose();
            tcs.TrySetResult();
        };

        _synthesizer.SpeakCompleted += onSpeakCompleted;

        _synthesizer.Speak(phrase);

        return tcs.Task;
    }

    private void CancelSpeaking(TaskCompletionSource tcs, string phrase)
    {
        _logger.LogInformation(Message.SpeechSynthesis_CancelledSaying, phrase);
        tcs.TrySetCanceled();
        _synthesizer.CancelAll();
    }
}
