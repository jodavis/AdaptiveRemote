namespace AdaptiveRemote.Services.Broadlink;

internal class AuthenticateResponsePayload : Payload
{
    public AuthenticateResponsePayload(Memory<byte> bytes)
        : base(bytes)
    { }

    public short DeviceID
    {
        get => GetShort(0x00);
    }

    public byte[] EncryptionKey
    {
        get => GetBytes(0x04, 16);
    }
}
