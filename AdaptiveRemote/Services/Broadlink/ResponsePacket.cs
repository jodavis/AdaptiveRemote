using System.Net;

namespace AdaptiveRemote.Services.Broadlink;

internal class ResponsePacket : Payload
{
    private const int ResponseHeaderSize = 0x38;

    internal ResponsePacket(EndPoint remoteEndPoint, Memory<byte> bytes)
        : base(bytes)
    {
        RemoteEndPoint = remoteEndPoint;
        Header = new ResponsePacketHeader(bytes.Slice(0, ResponseHeaderSize));
        Payload = new ResponsePacketHeader(bytes.Slice(ResponseHeaderSize));
    }

    protected override Span<byte> ChecksumSpanToIgnore => Span(0x20, 2);

    public EndPoint RemoteEndPoint
    {
        get;
    }

    public ResponsePacketHeader Header
    {
        get;
    }

    public Payload Payload
    {
        get;
    }
}
