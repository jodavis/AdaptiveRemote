using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Models;

public abstract class RemoteLayoutElement : MvvmObject
{
    public string Placement { get; }
    public string CSSID { get; }

    public RemoteLayoutElement(string id, string? placement = null)
    {
        Placement = placement ?? "N/A";
        CSSID = id;
    }

    public override abstract string ToString();
}
