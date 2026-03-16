namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// Decrypted response payload containing IR data captured during learning mode.
/// When the Broadlink device has successfully captured an IR signal, the decrypted
/// response contains the raw IR data starting at offset <c>0x04</c>.
/// </summary>
/// <seealso href="https://github.com/broadlink/broadlink/blob/master/broadlink/remote.py"/>
internal class LearnedDataResponsePayload : Payload
{
    private const int DataLengthSize = sizeof(short);
    private const int SkipSize = 4;

    public LearnedDataResponsePayload(Memory<byte> data)
        : base(data)
    { }

    private const int DataLengthIndex = 0x00;
    /// <summary>
    /// The length of the data plus the size of the command that preceeds it
    /// </summary>
    public short DataLength
    {
        get => GetShort(DataLengthIndex);
        set => Set(DataLengthIndex, value);
    }

    private const int DataIndex = DataLengthSize + SkipSize;
    /// <summary>
    /// The data to send
    /// </summary>
    public byte[] Data
    {
        get => GetBytes(DataIndex, DataLength);
        set => Set(DataIndex, value);
    }
}
