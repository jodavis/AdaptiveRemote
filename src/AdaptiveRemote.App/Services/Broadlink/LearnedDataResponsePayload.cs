namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// Response payload for the "get learned data" command, containing the captured IR signal data.
/// <see cref="https://github.com/mjg59/python-broadlink/blob/master/protocol.md"/>
/// </summary>
internal class LearnedDataResponsePayload : Payload
{
    public LearnedDataResponsePayload(Memory<byte> bytes)
        : base(bytes)
    { }

    private const int CommandAndDataLengthIndex = 0x00;
    /// <summary>
    /// The length of the IR data plus the command code field.
    /// </summary>
    public short CommandAndDataLength => GetShort(CommandAndDataLengthIndex);

    private const int CommandIndex = 0x02;
    /// <summary>
    /// The command code echoed back from the device.
    /// </summary>
    public int Command => GetInt(CommandIndex);

    private const int DataIndex = 0x06;
    /// <summary>
    /// The captured IR signal data.
    /// </summary>
    public byte[] Data => GetBytes(DataIndex);
}
