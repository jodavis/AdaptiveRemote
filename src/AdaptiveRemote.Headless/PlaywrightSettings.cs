namespace AdaptiveRemote.Headless;

public class PlaywrightSettings
{
    public int NavigationTimeout { get; set; } = 30000;

    public bool Headless { get; set; } = true;

    public string? TracesDir { get; set; } = null;

    public string? ExecutablePath { get; set; } = null;
}
