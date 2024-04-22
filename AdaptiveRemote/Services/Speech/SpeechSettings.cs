namespace AdaptiveRemote.Services.Speech;

internal class SpeechSettings
{
    public int ErrorRetryLimit { get; set; } = 10;
    public string Voice { get; set; } = "Microsoft Zira Desktop";
}
