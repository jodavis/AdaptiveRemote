namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// A fake speech synthesizer for testing and cross-platform scenarios
/// </summary>
internal class FakeSpeechSynthesizer : ISpeechSynthesizer
{
    private event EventHandler? _speakCompleted;

    public void SpeakAsync(string phrase)
    {
        // Simulate async speech completion
        Task.Run(async () =>
        {
            await Task.Delay(100);
            _speakCompleted?.Invoke(this, EventArgs.Empty);
        });
    }

    public void CancelAll() { }

    public IEnumerable<string> GetInstalledVoices() => ["Fake Voice"];

    public void SelectVoice(string fullName) { }

    public void SetSpeakingRate(int rate) { }

    public event EventHandler SpeakCompleted
    {
        add => _speakCompleted += value;
        remove => _speakCompleted -= value;
    }
}
