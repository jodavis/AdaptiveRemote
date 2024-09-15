namespace AdaptiveRemote.Models;

internal static class Phrases
{
    public static string Conversation_ImListening => "I'm listening...";
    public static string Conversation_ImSending => "Sending...";
    public static string Conversation_Sent(string command) => $"Sent {command}";
    public static string Conversation_Sent(string command, int repeat) => RepeatAction($"Sent {command}", repeat);
    public static string Conversation_StoppedListening => "Okay";
    public static string Conversation_YoureWelcome => "You're welcome!";
    public static string Conversation_WaitingForActivation => "Conversation system not started";
    public static string Conversation_ListeningForAttention => $"Say \"{Conversation_AttentionPhrase}\" to get my attention";
    public static string Conversation_AttentionPhrase => "Hey Remote";
    public static string Conversation_SystemFailed => "Conversation system error, try restarting";
    public static string Conversation_CommandDisabled(string name) => $"I can't do that. {name} is disabled.";
    public static string Conversation_ShuttingDown => "Shutting down...";
    public static string Conversation_Goodbye => "Goodbye";
    public static string Conversation_ImSorry => "I'm sorry.";

    internal static string RepeatAction(string action, int repeat)
        => repeat switch
        {
            1 => action,
            2 => $"{action} twice",
            _ => $"{action} {repeat} times"
        };
}
