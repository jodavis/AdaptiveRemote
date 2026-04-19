namespace AdaptiveRemote.Models;

internal class IRCommand : Command
{
    public IRCommand(
        string name,
        string? placement = null,
        string? label = null,
        string? cssid = null,
        string? glyph = null,
        string? reverse = null,
        string? speakName = null,
        string? speakPhrase = null)
        : base(name, placement, label, cssid, glyph, reverse, speakPhrase ?? Phrases.Conversation_Sent(speakName ?? name))
    {
    }
}
