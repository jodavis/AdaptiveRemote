namespace AdaptiveRemote.Services.Broadlink;

internal interface IDeviceLocator
{
    Task<ScanResponsePacket> FindDeviceAsync(CancellationToken cancellationToken);
}
