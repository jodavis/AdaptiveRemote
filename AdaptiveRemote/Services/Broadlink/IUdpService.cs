using System.Net;

namespace AdaptiveRemote.Services.Broadlink;

internal interface IUdpService
{
    Task<ResponsePacket> SendAsync(EndPoint remoteEndPoint, SendPacket packet, CancellationToken cancellationToken);

    IAsyncEnumerable<ResponsePacket> BroadcastAsync(SendPacket packet, CancellationToken cancellationToken);
}
