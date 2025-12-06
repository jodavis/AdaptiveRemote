namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// <see cref="https://github.com/mjg59/python-broadlink/blob/master/protocol.md#network-discovery"/>
/// </summary>
internal class ResponsePacketHeader : Payload
{
    internal ResponsePacketHeader(Memory<byte> bytes)
        : base(bytes)
    { }

    private const int ErrorCodeIndex = 0x22;
    /// <summary>
    /// 0 = Success, anything else means an error has occurred
    /// </summary>
    public short ErrorCode
    {
        get => GetShort(ErrorCodeIndex);
    }

    private const int NominalChecksumIndex = 0x20;
    /// <summary>
    /// THe checksum that was sent by the remote device
    /// </summary>
    public short NominalChecksum
    {
        get => GetShort(NominalChecksumIndex);
    }
}
