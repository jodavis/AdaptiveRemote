using System.Net.NetworkInformation;

namespace AdaptiveRemote.Services.Broadlink;

internal class SendPacketHeader : Payload
{
    private static readonly byte[] PreambleBytes = { 0x5a, 0xa5, 0xaa, 0x55, 0x5a, 0xa5, 0xaa, 0x55 };

    public SendPacketHeader()
        : base(new byte[0x38])
    {
        Preamble = PreambleBytes;
    }

    public byte[] Preamble
    {
        get => GetBytes(0x00, 8);
        set => Set(0x00, value);
    }

    public short DeviceType
    {
        get => GetShort(0x24);
        set => Set(0x24, value);
    }

    public short PacketType
    {
        get => GetShort(0x26);
        set => Set(0x26, value);
    }

    public short MessageCount
    {
        get => GetShort(0x28);
        set => Set(0x28, value);
    }

    public PhysicalAddress HostAddress
    {
        get => new PhysicalAddress(GetBytes(0x2A, 6));
        set => Set(0x2A, value.GetAddressBytes());
    }

    public short DeviceID
    {
        get => GetShort(0x30);
        set => Set(0x30, value);
    }

    public short PayloadChecksum
    {
        get => GetShort(0x34);
        set => Set(0x34, value);
    }

    public short PacketChecksum
    {
        get => GetShort(0x20);
        set => Set(0x20, value);
    }
}
