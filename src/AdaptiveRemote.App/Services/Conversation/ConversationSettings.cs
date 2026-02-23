namespace AdaptiveRemote.Services.Conversation;

internal class ConversationSettings
{
    public bool Fake { get; set; } = false;
    public int ErrorRetryLimit { get; set; } = 10;
    public string[] Voice { get; set; } = ["Zira"];
    public int CommandBufferSize { get; set; } = 2;
    public int SpeakingRate { get; set; } = 1;

    public bool RecordSamples { get; set; } = false;
    public string? RecordingOutputPath { get; set; } = default;
    public string? RecordingUserName { get; set; } = default;

    public int ListeningConfidenceThreshold { get; set; } = 30;
    public int WakeWordConfidenceThreshold { get; set; } = 70;
    public int HighConfidenceThreshold { get; set; } = 70;
}
