namespace AdaptiveRemote.Services.Broadlink;

internal class ResponsePacketHeader : Payload
{
    internal ResponsePacketHeader(Memory<byte> bytes)
        : base(bytes)
    { }

    public short ErrorCode
    {
        get => GetShort(0x22);
    }

    public short NominalChecksum
    {
        get => GetShort(0x20);
    }
}
