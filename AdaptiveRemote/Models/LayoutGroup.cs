namespace AdaptiveRemote.Models;

public class LayoutGroup : RemoteLayoutElement
{
    public IEnumerable<RemoteLayoutElement> Elements { get; }

    public LayoutGroup(string id, IEnumerable<RemoteLayoutElement> elements, string? placement = null)
        : base(id, placement)
    {
        Elements = elements;
    }

    public override string ToString() => $"LayoutGroup {Placement}/{CSSID}";
}
