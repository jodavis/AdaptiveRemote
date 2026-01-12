using System.Diagnostics.CodeAnalysis;
using System.Speech.Synthesis;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Conversation;

[ExcludeFromCodeCoverage(Justification = "Simple wrapper around System.Speech.SpeechSynthesizer")]
internal class SpeechSynthesizerWrapper : ISpeechSynthesizer, IDisposable
{
    private readonly SpeechSynthesizer _speechSynthesizer = new();
    private readonly ILogger<SpeechSynthesizer> _logger;
    private EventHandler? _speakCompleted;

    public SpeechSynthesizerWrapper(IAudioConfigurationService audioConfiguration, ILogger<SpeechSynthesizer> logger)
    {
        _logger = logger;

        _speechSynthesizer.BookmarkReached += OnBookmarkReached;
        _speechSynthesizer.SpeakCompleted += OnSpeakCompleted;
        _speechSynthesizer.SpeakProgress += OnSpeakProcess;
        _speechSynthesizer.SpeakStarted += OnSpeakStarted;
        _speechSynthesizer.StateChanged += OnStateChanged;
        _speechSynthesizer.VoiceChange += OnVoiceChange;

        _speechSynthesizer.SpeakCompleted += BroadcastSpeakCompleted;

        audioConfiguration.Configure(_speechSynthesizer);
    }

    private void BroadcastSpeakCompleted(object? sender, SpeakCompletedEventArgs e)
        => _speakCompleted?.Invoke(this, EventArgs.Empty);

    public void CancelAll() => _speechSynthesizer.SpeakAsyncCancelAll();
    public void Speak(string textToSpeak) => _speechSynthesizer.SpeakAsync(textToSpeak);
    public void Dispose() => _speechSynthesizer.Dispose();
    public IEnumerable<string> GetInstalledVoices() => _speechSynthesizer.GetInstalledVoices().Select(x => x.VoiceInfo.Name);
    public void SetSpeakingRate(int rate) => _speechSynthesizer.Rate = rate;
    public void SelectVoice(string name) => _speechSynthesizer.SelectVoice(name);

    public event EventHandler SpeakCompleted
    {
        add => _speakCompleted += value;
        remove => _speakCompleted -= value;
    }

    private void OnBookmarkReached(object? sender, BookmarkReachedEventArgs e)
        => _logger.LogInformation($"Bookmark {e.Bookmark} reached in '{e.Prompt}' at {e.AudioPosition}");
    private void OnSpeakCompleted(object? sender, SpeakCompletedEventArgs e)
        => _logger.LogInformation($"Speaking completed (Cancelled: {e.Cancelled}, Error: {e.Error})");
    private void OnSpeakProcess(object? sender, SpeakProgressEventArgs e)
        => _logger.LogInformation($"Speaking progress: '{e.Text}' at {e.AudioPosition}");
    private void OnSpeakStarted(object? sender, SpeakStartedEventArgs e)
        => _logger.LogInformation($"Speaking started");
    private void OnStateChanged(object? sender, StateChangedEventArgs e)
        => _logger.LogInformation($"State chaned to {e.State} from {e.PreviousState}");
    private void OnVoiceChange(object? sender, VoiceChangeEventArgs e)
        => _logger.LogInformation($"Voice changed to '{e.Voice.Name}'");
}
