namespace AdaptiveRemote.Models;

public class TiVoCommand : Command
{
    public TiVoCommand(
        string name,
        string? commandId = null,
        string? placement = null,
        string? label = null,
        string? cssid = null,
        string? glyph = null)
        : base(name, placement, label, cssid ?? commandId, glyph)
    {
        CommandId = commandId ?? name.ToUpperInvariant();
    }

    public string CommandId { get; }
}
