namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// Wrap UDP behavior, including retries and error handling
/// </summary>
internal interface IUdpService
{
    /// <summary>
    /// Send a <see cref="SendPacket"/> to a specific remote endpoint.
    /// </summary>
    /// <param name="packet">The packet information to send</param>
    /// <returns>A task that completes when a response is received from the remote endpoint</returns>
    Task<ResponsePacket> SendAsync(SendPacket packet, CancellationToken cancellationToken);

    /// <summary>
    /// Broadcast a <see cref="ScanRequestPacket"/> to all devices on the local network
    /// </summary>
    /// <param name="packet">THe packet of information to send</param>
    /// <returns>An asynchronous enumerator that returns any responses from devices on the network</returns>
    IAsyncEnumerable<ScanResponsePacket> BroadcastAsync(ScanRequestPacket packet, CancellationToken cancellationToken);
}
