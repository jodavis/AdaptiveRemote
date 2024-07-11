namespace AdaptiveRemote.Services.Broadlink;

internal interface IDeviceLocator
{
    Task<ScanResponsePacket> FindDevice(CancellationToken cancellationToken);
}
