namespace AdaptiveRemote.Services;

internal static class Phrases
{
    public static string Speech_ImListening => "I'm listening...";
    public static string Speech_Sending(string command) => $"Sending {command}";
    public static string Speech_StoppedListening => "Okay";
    public static string Speech_WaitingForActivation => "Speech system not active";
    public static string Speech_ListeningForAttention => $"Say \"{Speech_Attention}\" to start listening";
    public static string Speech_Attention => "Hey Remote";
    public static string Speech_ListeningSystemFailed => "Listening error, try restarting";
    public static string Speech_ImSending => "Sending...";
}
