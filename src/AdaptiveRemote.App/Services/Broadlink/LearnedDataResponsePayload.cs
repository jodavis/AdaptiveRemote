namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// Decrypted response payload containing IR data captured during learning mode.
/// When the Broadlink device has successfully captured an IR signal, the decrypted
/// response contains the raw IR data starting at offset <c>0x04</c>.
/// </summary>
/// <seealso href="https://github.com/broadlink/broadlink/blob/master/broadlink/remote.py"/>
internal class LearnedDataResponsePayload : Payload
{
    private const int DataIndex = 0x04;

    public LearnedDataResponsePayload(Memory<byte> buffer)
        : base(buffer)
    { }

    /// <summary>
    /// The captured IR data bytes, which can be Base64-encoded and stored for later playback.
    /// </summary>
    public byte[] Data => GetBytes(DataIndex);
}
