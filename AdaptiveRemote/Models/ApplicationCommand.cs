namespace AdaptiveRemote.Models;

public class ApplicationCommand : Command
{
    public ApplicationCommand(
        string name,
        string? placement = null,
        string? label = null,
        string? cssid = null,
        string? glyph = null,
        string? speakPhrase = null)
        : base(name, placement, label, cssid, glyph, speakPhrase)
    {
    }
}
