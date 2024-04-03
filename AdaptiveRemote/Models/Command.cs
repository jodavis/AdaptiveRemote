namespace AdaptiveRemote.Models;

public abstract class Command : RemoteLayoutElement
{
    protected Command(string group, string label)
        : this(group, label, label.ToUpperInvariant())
    { }

    protected Command(string group, string label, string id)
        : base(group, id)
    {
        Label = label;
    }

    public string Label { get; }
}
