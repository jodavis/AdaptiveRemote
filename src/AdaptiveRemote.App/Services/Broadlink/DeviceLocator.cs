namespace AdaptiveRemote.Services.Broadlink;

internal class DeviceLocator : IDeviceLocator
{
    private readonly IUdpService _udpService;

    public DeviceLocator(IUdpService udpService)
    {
        _udpService = udpService;
    }

    async Task<ScanResponsePacket> IDeviceLocator.FindDeviceAsync(CancellationToken cancellationToken)
    {
        ScanRequestPacket requestPacket = new()
        {
            RequestTime = DateTimeOffset.Now
        };

        CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await foreach (ScanResponsePacket response in _udpService.BroadcastAsync(requestPacket, stop.Token))
            {
                return response;
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw Errors.DeviceLocator_DeviceNotFound();
        }
        finally
        {
            await stop.CancelAsync();
        }
    }
}
