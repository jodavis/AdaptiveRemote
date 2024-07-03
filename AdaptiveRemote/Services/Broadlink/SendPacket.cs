namespace AdaptiveRemote.Services.Broadlink;

internal class SendPacket : Payload
{
    public SendPacket(SendPacketHeader header, Payload payload)
            : base(header, payload)
    {
        Header = header;
        Payload = payload;
    }

    public SendPacketHeader Header { get; }
    public Payload Payload { get; }
}
