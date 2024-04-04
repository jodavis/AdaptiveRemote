using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Models;

public abstract class RemoteLayoutElement : MvvmObject
{
    public string Group { get; }
    public string ID { get; }

    public RemoteLayoutElement(string group, string id)
    {
        Group = group;
        ID = id;
    }
}
