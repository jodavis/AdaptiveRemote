namespace AdaptiveRemote.Models;

internal class BroadlinkCommand : Command
{
    public BroadlinkCommand(
        string name,
        string? placement = null,
        string? label = null,
        string? cssid = null,
        string? glyph = null,
        string[]? alternates = null)
        : base(name, placement, label, cssid, glyph)
    {
    }
}
