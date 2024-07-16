using System.Net;

namespace AdaptiveRemote.Services.Broadlink;

internal class SendPacket : Payload
{
    public SendPacket(EndPoint remoteEndPoint, SendPacketHeader header, Payload payload)
            : base(header, payload)
    {
        RemoteEndPoint = remoteEndPoint;
        Header = header;
        Payload = payload;
    }

    public EndPoint RemoteEndPoint { get; }
    public SendPacketHeader Header { get; }
    public Payload Payload { get; }

    public override string ToString() => $"Device {Header.DeviceID:X4}, Message {Header.MessageCount:X4}";
}
