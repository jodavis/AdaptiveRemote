using System.Net;

namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// <see cref="https://github.com/mjg59/python-broadlink/blob/master/protocol.md#network-discovery"/>
/// </summary>
internal class SendPacket : Payload
{
    public SendPacket(EndPoint remoteEndPoint, SendPacketHeader header, Payload payload)
            : base(header, payload)
    {
        RemoteEndPoint = remoteEndPoint;
        Header = header;
        Payload = payload;
    }

    /// <summary>
    /// The remote EndPoint to send the packet to.
    /// </summary>
    public EndPoint RemoteEndPoint { get; }

    /// <summary>
    /// THe first part of the packet is header information.
    /// </summary>
    public SendPacketHeader Header { get; }

    /// <summary>
    /// The second part of the packet is the payload to send to the device.
    /// </summary>
    public Payload Payload { get; }

    public override string ToString() => $"Device {Header.DeviceID:X4}, Message {Header.MessageCount:X4}";
}
