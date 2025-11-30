namespace AdaptiveRemote.Models;

public class LifecycleCommand : Command
{
    public LifecycleCommand(
        string name,
        string? placement = null,
        string? label = null,
        string? cssid = null,
        string? glyph = null,
        string? reverse = null,
        string? speakPhrase = null)
        : base(name, placement, label, cssid, glyph, reverse, speakPhrase)
    {
    }
}
