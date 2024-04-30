namespace AdaptiveRemote.Models;

internal class TvCommand : Command
{
    public TvCommand(
        string name,
        string? placement = null,
        string? label = null,
        string? cssid = null,
        string? glyph = null)
        : base(name, placement, label, cssid, glyph)
    { }
}
