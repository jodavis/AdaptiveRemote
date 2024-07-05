using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Broadlink;

internal class UdpService : IUdpService
{
    private readonly ISocketFactory _socketFactory;
    private readonly BroadlinkSettings _settings;
    private readonly ILogger<UdpService> _logger;

    public UdpService(ISocketFactory socketFactory, IOptions<BroadlinkSettings> settings, ILogger<UdpService> logger)
    {
        _socketFactory = socketFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    IAsyncEnumerable<ResponsePacket> IUdpService.BroadcastAsync(SendPacket packet, CancellationToken cancellationToken)
    {
        // TODO: Implement this
        throw new NotImplementedException();
    }

    Task<ResponsePacket> IUdpService.SendAsync(EndPoint remoteEndPoint, SendPacket packet, CancellationToken cancellationToken)
    {
        ISocket socket = _socketFactory.Create();

        socket.SetTimeout(TimeSpan.FromSeconds(_settings.SendTimeout));

        // TODO: Add retry (or wait until needed?)

        // TODO: Await this
        // TODO: Pass cancellation
#pragma warning disable CA2012 // Use ValueTasks correctly -- this is work in progress
        socket.SendToAsync(packet.GetBuffer(), remoteEndPoint, default);
#pragma warning restore CA2012 // Use ValueTasks correctly
                              // TODO: Throw if cancelled

        Memory<byte> buffer = new byte[0x400];

        // TODO: Await this
        // TODO: Pass cancellation
#pragma warning disable CA2012 // Use ValueTasks correctly -- this is work in progress
        SocketReceiveFromResult result = socket.ReceiveFromAsync(buffer, remoteEndPoint, default).Result;
#pragma warning restore CA2012 // Use ValueTasks correctly
                              // TODO: Throw if cancelled
        buffer = buffer.Slice(0, result.ReceivedBytes);

        return Task.FromResult(new ResponsePacket(remoteEndPoint, buffer));
    }
}
