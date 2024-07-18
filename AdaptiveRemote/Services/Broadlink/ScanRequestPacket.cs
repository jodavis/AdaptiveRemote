namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// <see cref="https://github.com/mjg59/python-broadlink/blob/master/protocol.md#network-discovery"/>
/// </summary>
internal class ScanRequestPacket : Payload
{
    private const int SomeRandomSixIndex = 0x26;
    public ScanRequestPacket()
        : base(0x30)
    {
        // This byte needs to be 6 for some undocumented reason
        Set(SomeRandomSixIndex, 6);
    }

    private const int RequestTimeOffsetPosition = 0x08;
    private const int RequestTimePosition = 0x0E;
    /// <summary>
    /// The time when the request was sent, encoded in some weird format
    /// </summary>
    public DateTime RequestTime
    {
        get
        {
            byte[] bytes = GetBytes(RequestTimePosition, 6);
            return new DateTime(DateTime.Now.Year, bytes[5], bytes[4], bytes[2], bytes[1], bytes[0]);
        }
        set
        {
            long offset = value.ToFileTime() - value.ToFileTimeUtc();
            Set(RequestTimeOffsetPosition, (int)offset);

            Set(RequestTimePosition, new byte[]
            {
                (byte)value.Second,
                (byte)value.Minute,
                (byte)value.Hour,
                (byte)value.DayOfWeek,
                (byte)value.Day,
                (byte)value.Month,
            });
        }
    }

    private const int LocalIPAddressPosition = 0x18;
    /// <summary>
    /// The IP address of the local device, where responses should be sent
    /// </summary>
    public byte[] LocalIPAddress
    {
        get => GetBytes(LocalIPAddressPosition, 4);
        set => Set(LocalIPAddressPosition, value);
    }

    private const int LocalPortPosition = 0x1C;
    /// <summary>
    /// THe port where responses should be sent
    /// </summary>
    public short LocalPort
    {
        get => GetShort(LocalPortPosition);
        set => Set(LocalPortPosition, value);
    }

    private const int ChecksumPosition = 0x20;
    /// <summary>
    /// Checksum of this request
    /// </summary>
    public short Checksum
    {
        get => GetShort(ChecksumPosition);
        set => Set(ChecksumPosition, value);
    }
}
