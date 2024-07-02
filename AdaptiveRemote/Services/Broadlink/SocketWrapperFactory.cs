using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace AdaptiveRemote.Services.Broadlink;

[ExcludeFromCodeCoverage(Justification = "Simple wrapper around System.Net.Socket")]
internal class SocketWrapperFactory : ISocketFactory
{
    ISocket ISocketFactory.Create(EndPoint targetEndPoint, SocketOptionName options)
    {
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        foreach (SocketOptionName option in Enum.GetValues(typeof(SocketOptionName)))
        {
            if ((options & option) == option)
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, option, true);
            }
        }
        socket.Bind(targetEndPoint);

        return new SocketWrapper(socket);
    }

    ISocket ISocketFactory.CreateForBroadcast() => new SocketWrapper(new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp));

    private class SocketWrapper : ISocket
    {
        private Socket _socket;

        public SocketWrapper(Socket socket)
        {
            _socket = socket;
        }

        ValueTask<int> ISocket.SendToAsync(ReadOnlyMemory<byte> packet, EndPoint endPoint, CancellationToken cancellationToken)
            => _socket.SendToAsync(packet, endPoint, cancellationToken);
        ValueTask<int> ISocket.ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
            => _socket.ReceiveAsync(buffer, cancellationToken);
        ValueTask<SocketReceiveFromResult> ISocket.ReceiveFromAsync(Memory<byte> buffer, EndPoint remoteEP, CancellationToken cancellationToken)
            => _socket.ReceiveFromAsync(buffer, remoteEP, cancellationToken);

        void ISocket.SetTimeout(TimeSpan time_left)
        {
            _socket.SendTimeout = (int)time_left.TotalMilliseconds;
            _socket.ReceiveTimeout = (int)time_left.TotalMilliseconds;
        }

        void ISocket.Close() => _socket.Close();
        void IDisposable.Dispose() => _socket.Dispose();
    }
}
