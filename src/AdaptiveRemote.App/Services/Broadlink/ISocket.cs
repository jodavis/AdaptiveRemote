using System.Net;
using System.Net.Sockets;

namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// Abstraction for the .NET Socket class, used for unit testing services
/// that require socket connections
/// </summary>
public interface ISocket : IDisposable
{
    /// <summary>
    /// Gets the local endpoint of the socket.
    /// </summary>
    EndPoint? LocalEndPoint { get; }

    /// <summary>
    /// Sends data to the specified remote host.
    /// </summary>
    /// <param name="packet">The buffer for the data to send.</param>
    /// <param name="endPoint">The remote host to which to send the data.</param>
    /// <returns>
    /// An asynchronous task that completes with the number of bytes sent.
    /// </returns>
    ValueTask<int> SendToAsync(ReadOnlyMemory<byte> packet, EndPoint endPoint, CancellationToken cancellationToken);

    /// <summary>
    /// Receives data and returns the endpoint of the sending host.
    /// </summary>
    /// <param name="buffer">The buffer for the received data.</param>
    /// <param name="endPoint">An endpoint of the same type as the endpoint of the remote host.</param>
    /// <returns>
    /// An asynchronous task that completes with a System.Net.Sockets.SocketReceiveFromResult
    /// containing the number of bytes received and the endpoint of the sending host.
    /// </returns>
    ValueTask<SocketReceiveFromResult> ReceiveFromAsync(Memory<byte> buffer, EndPoint endPoint, CancellationToken cancellationToken);

    internal interface Factory
    {
        /// <summary>
        /// Create a socket configured for broadcast to all devices on the local network.
        /// </summary>
        ISocket CreateForBroadcast();

        /// <summary>
        /// Create a socket configured for sending data to a specific device.
        /// </summary>
        ISocket Create();
    }
}
