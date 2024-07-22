namespace AdaptiveRemote.Services.Broadlink;

public class BroadlinkSettings
{
    /// <summary>
    /// Amount of time, in seconds, that we will wait for a response to sending data.
    /// </summary>
    public int SendTimeout { get; set; } = 5;

    /// <summary>
    /// Amoutn of time, in seconds, that we will wait for a response when scanning
    /// for devices.
    /// </summary>
    public double ScanTimeout { get; set; } = 3;
}
