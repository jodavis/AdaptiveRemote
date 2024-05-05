namespace AdaptiveRemote.Services.Conversation;

internal class ConversationSettings
{
    public int ErrorRetryLimit { get; set; } = 10;
    public string[] Voice { get; set; } = ["Jenny", "Zira"];
    public int CommandBufferSize { get; set; } = 2;

    public bool RecordSamples { get; set; } = false;
    public string? RecordingOutputPath { get; set; } = default;
    public string? RecordingUserName { get; set; } = default;
}
