namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// <see cref="https://github.com/mjg59/python-broadlink/blob/master/protocol.md#network-discovery"/>
/// </summary>
internal class AuthenticateRequestPayload : Payload
{
    public AuthenticateRequestPayload()
        : base(0x50)
    {
        // None of these are actually necessary, but the protocol calls for them
        Set(0x04, "1111111111111111");
        Set(0x1E, 1);
        Set(0x2D, 1);
        Set(0x30, "Test 1");
    }
}
