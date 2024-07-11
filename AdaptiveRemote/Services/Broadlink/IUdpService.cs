using System.Net;

namespace AdaptiveRemote.Services.Broadlink;

internal interface IUdpService
{
    Task<ResponsePacket> SendAsync(EndPoint remoteEndPoint, SendPacket packet, CancellationToken cancellationToken);

    IAsyncEnumerable<ScanResponsePacket> BroadcastAsync(ScanRequestPacket packet, CancellationToken cancellationToken);
}
