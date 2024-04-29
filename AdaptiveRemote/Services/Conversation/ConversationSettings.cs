namespace AdaptiveRemote.Services.Conversation;

internal class ConversationSettings
{
    public int ErrorRetryLimit { get; set; } = 10;
    public string[] Voice { get; set; } = ["Jenny", "Zira"];
}
