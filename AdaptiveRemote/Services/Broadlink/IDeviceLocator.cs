namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// Locate a Broadlink device on the local network
/// </summary>
internal interface IDeviceLocator
{
    /// <summary>
    /// Locate a Broadlink device on the local network and return its
    /// connection information.
    /// </summary>
    /// <returns>A Task that completes when a Broadlink device has been located</returns>
    Task<ScanResponsePacket> FindDeviceAsync(CancellationToken cancellationToken);
}
