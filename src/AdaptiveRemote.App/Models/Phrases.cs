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
    public static string Conversation_Goodbye => "Goodbye";
    public static string Conversation_ImSorry => "I'm sorry.";
    public static string Conversation_DidYouSay(string speech) => $"Did you say '{speech}'?";

    public static string Startup_StartingApplication => "Starting application";
    public static string Startup_BuildingServiceGraph => "Building service graph";
    public static string Startup_StartingServices => "Starting services";
    public static string Startup_ConnectingToBroadlink => "Connecting to Broadlink device";
    public static string Startup_ConnectingToTiVo => "Connecting to TiVo";
    public static string Startup_Starting(string service) => $"Starting {service}";
    public static string Cleanup_StoppingApplication => "Stopping application";
    public static string Cleanup_CleaningUpApplication => "Cleaning up application";
    public static string Cleanup_CleaningUp(string service) => $"Cleaning up {service}";
    public static string Cleanup_ShuttingDown => "Shutting down...";

    public static string Broadlink_ProgrammingCommand(string command) =>
        $"# Programming '{command}'\n\nPoint your remote at the Broadlink device and press {command}.";

    public static string Broadlink_NotConnected(string command) =>
        $"Cannot program {command}: the Broadlink service is not connected.";

    public static string Ellipsis(string activity) => $"{activity}...";
    public static string ErrorWhile(string activity) => $"Error while {activity}";

    internal static string RepeatAction(string action, int repeat)
        => repeat switch
        {
            1 => action,
            2 => $"{action} twice",
            _ => $"{action} {repeat} times"
        };
}
