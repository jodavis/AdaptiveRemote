using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace AdaptiveRemote.Services.Broadlink;

[ExcludeFromCodeCoverage(Justification = "Simple wrapper around System.Net.Socket")]
internal class SocketWrapper : ISocket
{
    private Socket _socket;

    private SocketWrapper(Socket socket)
    {
        _socket = socket;
    }

    ValueTask<int> ISocket.SendToAsync(ReadOnlyMemory<byte> packet, EndPoint endPoint, CancellationToken cancellationToken)
        => _socket.SendToAsync(packet, endPoint, cancellationToken);
    ValueTask<int> ISocket.ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        => _socket.ReceiveAsync(buffer, cancellationToken);
    ValueTask<SocketReceiveFromResult> ISocket.ReceiveFromAsync(Memory<byte> buffer, EndPoint remoteEP, CancellationToken cancellationToken)
        => _socket.ReceiveFromAsync(buffer, remoteEP, cancellationToken);

    void IDisposable.Dispose()
    {
        _socket.Close();
        _socket.Dispose();
    }

    internal class Factory : ISocketFactory
    {
        ISocket ISocketFactory.CreateForBroadcast()
        {
            Socket socket = CreateUdpSocket();
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            socket.Bind(new IPEndPoint(0, 0));

            return new SocketWrapper(socket);
        }

        ISocket ISocketFactory.Create() => new SocketWrapper(CreateUdpSocket());

        private static Socket CreateUdpSocket() => new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    }
}
