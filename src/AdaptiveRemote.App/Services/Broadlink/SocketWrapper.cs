using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace AdaptiveRemote.Services.Broadlink;

[ExcludeFromCodeCoverage(Justification = "Simple wrapper around System.Net.Socket")]
internal class SocketWrapper : ISocket
{
    private readonly Socket _socket;

    private SocketWrapper(Socket socket)
    {
        _socket = socket;
    }

    ValueTask<int> ISocket.SendToAsync(ReadOnlyMemory<byte> packet, EndPoint endPoint, CancellationToken cancellationToken)
        => _socket.SendToAsync(packet, endPoint, cancellationToken);
    ValueTask<SocketReceiveFromResult> ISocket.ReceiveFromAsync(Memory<byte> buffer, EndPoint remoteEP, CancellationToken cancellationToken)
        => _socket.ReceiveFromAsync(buffer, remoteEP, cancellationToken);

    void IDisposable.Dispose()
    {
        _socket.Close();
        _socket.Dispose();
    }

    internal class Factory : ISocket.Factory
    {
        ISocket ISocket.Factory.CreateForBroadcast()
        {
            Socket socket = CreateUdpSocket();
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            socket.Bind(new IPEndPoint(0, 0));

            return new SocketWrapper(socket);
        }

        ISocket ISocket.Factory.Create() => new SocketWrapper(CreateUdpSocket());

        private static Socket CreateUdpSocket() => new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    }
}
