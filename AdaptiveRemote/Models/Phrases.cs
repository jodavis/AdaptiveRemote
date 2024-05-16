namespace AdaptiveRemote.Models;

internal static class Phrases
{
    public static string Conversation_ImListening => "I'm listening...";
    public static string Conversation_ImSending => "Sending...";
    public static string Conversation_Sent(string command) => $"Sent {command}";
    public static string Conversation_StoppedListening => "Okay";
    public static string Conversation_WaitingForActivation => "Conversation system not started";
    public static string Conversation_ListeningForAttention => $"Say \"{Conversation_AttentionPhrase}\" to get my attention";
    public static string Conversation_AttentionPhrase => "Hey Remote";
    public static string Conversation_SystemFailed => "Conversation system error, try restarting";
}
