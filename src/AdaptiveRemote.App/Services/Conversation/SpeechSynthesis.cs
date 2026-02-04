using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Conversation;

internal class SpeechSynthesis : ISpeechSynthesis
{
    private readonly ISpeechSynthesizer _synthesizer;
    private readonly IListeningController _listeningController;
    private readonly MessageLogger _logger;

    private int _isSpeaking = 0;

    public SpeechSynthesis(ISpeechSynthesizer synthesizer, IListeningController listeningController, IOptionsSnapshot<ConversationSettings> settings, ILogger<SpeechSynthesis> logger)
    {
        _synthesizer = synthesizer;
        _listeningController = listeningController;
        _logger = new(logger);

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
                    _logger.SpeechSynthesis_SelectedVoice(installedVoice);
                    return;
                }
            }
            _logger.SpeechSynthesis_VoiceNotFound(voiceName);
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
            _logger.SpeechSynthesis_AlreadySpeaking(phrase);
            throw new InvalidOperationException($"Cannot say '{phrase}'; another phrase is already in progress");
        }

        using (_listeningController.Pause())
        {
            try
            {
                _logger.SpeechSynthesis_Saying(phrase);
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
        _logger.SpeechSynthesis_CancelledSaying(phrase);
        tcs.TrySetCanceled();
        _synthesizer.CancelAll();
    }
}
