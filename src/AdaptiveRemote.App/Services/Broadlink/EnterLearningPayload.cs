namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// Payload for entering IR learning mode on the Broadlink device.
/// Sent as the payload of a <c>0x6A</c> packet type to put the device into learning mode,
/// where it will capture the next IR signal it receives.
/// </summary>
/// <seealso href="https://github.com/broadlink/broadlink/blob/master/broadlink/remote.py"/>
internal class EnterLearningPayload : CommandPayload
{
    private const int EnterLearningCommand = 0x3;

    public EnterLearningPayload()
        : base(EnterLearningCommand, [])
    { }
}
