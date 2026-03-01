namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// Payload for polling the Broadlink device for a captured IR code during learning mode.
/// Sent repeatedly during the learning sequence until the device responds with a captured IR code,
/// the operation times out, or the user cancels.
/// </summary>
/// <seealso href="https://github.com/broadlink/broadlink/blob/master/broadlink/remote.py"/>
internal class CheckLearnedDataPayload : CommandPayload
{
    private const int CheckLearnedDataCommand = 0x4;

    public CheckLearnedDataPayload()
        : base(CheckLearnedDataCommand, [])
    { }
}
