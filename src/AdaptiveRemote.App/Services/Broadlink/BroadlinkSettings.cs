namespace AdaptiveRemote.Services.Broadlink;

public class BroadlinkSettings
{
    /// <summary>
    /// If true, Broadlink services will not start, but all Broadlink commands will
    /// be enabled as no-ops.
    /// </summary>
    public bool Fake { get; set; } = false;

    /// <summary>
    /// Amount of time, in seconds, that we will wait for a response to sending data.
    /// </summary>
    public int SendTimeout { get; set; } = 5;

    /// <summary>
    /// Amount of time, in seconds, that we will wait for a response when scanning
    /// for devices.
    /// </summary>
    public double ScanTimeout { get; set; } = 3;

    /// <summary>
    /// Amount of time, in seconds, to wait between polling the device for a learned IR code.
    /// </summary>
    public double LearnPollInterval { get; set; } = 1;

    /// <summary>
    /// Maximum amount of time, in seconds, to wait for the device to return a learned IR code.
    /// This should be set significantly longer than the device's own learning mode timeout so
    /// that the device can exit learning mode naturally before the application gives up. Defaults
    /// to 120 seconds (2 minutes).
    /// </summary>
    public double LearnTimeout { get; set; } = 120;

    /// <summary>
    /// Optional discovery port override for testing. If not set, uses port 80.
    /// </summary>
    public int? DiscoveryPort { get; set; }

    /// <summary>
    /// Optional discovery address override for testing. If not set, uses broadcast address.
    /// </summary>
    public string? DiscoveryAddress { get; set; }
}
