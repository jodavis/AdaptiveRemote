using System.Net.NetworkInformation;

namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// <see cref="https://github.com/mjg59/python-broadlink/blob/master/protocol.md#network-discovery"/>
/// </summary>
internal class SendPacketHeader : Payload
{
    private static readonly byte[] PreambleBytes =
    {
        0x5a, 0xa5, 0xaa, 0x55,
        0x5a, 0xa5, 0xaa, 0x55
    };

    public SendPacketHeader()
        : base(new byte[0x38])
    {
        Preamble = PreambleBytes;
    }

    private const int PreambleIndex = 0x00;
    /// <summary>
    /// A preamble that must be sent with every packet.
    /// </summary>
    public byte[] Preamble
    {
        get => GetBytes(PreambleIndex, 8);
        set => Set(PreambleIndex, value);
    }

    private const int PacketChecksumIndex = 0x20;
    /// <summary>
    /// Checksum for the entire packet
    /// </summary>
    public short PacketChecksum
    {
        get => GetShort(PacketChecksumIndex);
        set => Set(PacketChecksumIndex, value);
    }

    private const int DeviceTypeIndex = 0x24;
    /// <summary>
    /// A 16-bit code representing the type of Broadlink device
    /// </summary>
    public short DeviceType
    {
        get => GetShort(DeviceTypeIndex);
        set => Set(DeviceTypeIndex, value);
    }

    private const int PacketTypeIndex = 0x26;
    /// <summary>
    /// A 16-bit code representing the type of packet
    /// </summary>
    public short PacketType
    {
        get => GetShort(PacketTypeIndex);
        set => Set(PacketTypeIndex, value);
    }

    private const int MessageCountIndex = 0x28;
    /// <summary>
    /// An index that must be incremented for every message sent to the device.
    /// </summary>
    public short MessageCount
    {
        get => GetShort(MessageCountIndex);
        set => Set(MessageCountIndex, value);
    }

    private const int HostAddressIndex = 0x2A;
    /// <summary>
    /// THe MAC address of the target device
    /// </summary>
    public PhysicalAddress HostAddress
    {
        get => new PhysicalAddress(GetBytes(HostAddressIndex, 6));
        set => Set(HostAddressIndex, value.GetAddressBytes());
    }

    private const int DeviceIDIndex = 0x30;
    /// <summary>
    /// The 16-bit ID of the device
    /// </summary>
    public short DeviceID
    {
        get => GetShort(DeviceIDIndex);
        set => Set(DeviceIDIndex, value);
    }

    private const int PayloadChecksumIndex = 0x34;
    /// <summary>
    /// The checksum of the unencrypted payload, used to ensure the payload
    /// is decrypted correctly
    /// </summary>
    public short PayloadChecksum
    {
        get => GetShort(PayloadChecksumIndex);
        set => Set(PayloadChecksumIndex, value);
    }
}
