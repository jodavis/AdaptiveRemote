namespace AdaptiveRemote.Services.Broadlink;

[Serializable]
internal class BroadlinkException : Exception
{
    public BroadlinkException(string message)
        : base(message)
    { }
}
