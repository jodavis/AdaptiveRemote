namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// <see cref="https://github.com/mjg59/python-broadlink/blob/master/protocol.md#network-discovery"/>
/// </summary>
internal class AuthenticateResponsePayload : Payload
{
    public AuthenticateResponsePayload(Memory<byte> bytes)
        : base(bytes)
    { }

    private const int DeviceIDIndex = 0x00;

    /// <summary>
    /// A code representing the ID of this device.
    /// </summary>
    public short DeviceID
    {
        get => GetShort(DeviceIDIndex);
    }

    private const int EncryptionKeyIndex = 0x04;
    private const int EncryptionKeyLength = 16;
    /// <summary>
    /// The encryption key used to encode future messages to this device.
    /// </summary>
    public byte[] EncryptionKey
    {
        get => GetBytes(EncryptionKeyIndex, EncryptionKeyLength);
    }
}
