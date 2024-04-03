namespace AdaptiveRemote.Models;

public class LayoutGroup : RemoteLayoutElement
{
    public IEnumerable<RemoteLayoutElement> Elements { get; }

    public LayoutGroup(string group, string name, IEnumerable<RemoteLayoutElement> elements)
        : base(group, name)
    {
        Elements = elements;
    }
}
