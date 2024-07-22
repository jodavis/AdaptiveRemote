namespace AdaptiveRemote.Models;

internal class IRCommand : Command
{
    public IRCommand(
        string name,
        string data,
        string? placement = null,
        string? label = null,
        string? cssid = null,
        string? glyph = null)
        : base(name, placement, label, cssid, glyph)
    {
        Data = data;
    }

    public string Data { get; }
}
