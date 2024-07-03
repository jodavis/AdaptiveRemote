namespace AdaptiveRemote.Services.Broadlink;

internal class AuthenticateRequestPayload : Payload
{
    public AuthenticateRequestPayload()
        : base(0x50)
    {
        Set(0x04, "1111111111111111");
        Set(0x1E, 1);
        Set(0x2D, 1);
        Set(0x30, "Test 1");
    }
}
