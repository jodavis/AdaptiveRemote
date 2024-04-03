namespace AdaptiveRemote.Models;

public abstract class RemoteLayoutElement
{
    public string Group { get; }
    public string ID { get; }

    public RemoteLayoutElement(string group, string id)
    {
        Group = group;
        ID = id;
    }
}
