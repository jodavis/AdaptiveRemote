using System.Net;
using System.Net.Sockets;

namespace AdaptiveRemote.Services.Broadlink;

public interface ISocket : IDisposable
{
    void SetTimeout(TimeSpan timeout);
    ValueTask<int> SendToAsync(ReadOnlyMemory<byte> packet, EndPoint endPoint, CancellationToken cancellationToken);
    ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    ValueTask<SocketReceiveFromResult> ReceiveFromAsync(Memory<byte> buffer, EndPoint endPoint, CancellationToken cancellationToken);
    void Close();
}
