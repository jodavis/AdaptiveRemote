namespace AdaptiveRemote.Models;

internal class IRCommand : Command
{
    public IRCommand(
        string name,
        string data,
        string? placement = null,
        string? label = null,
        string? cssid = null,
        string? glyph = null,
        string? reverse = null,
        string? speakPhrase = null)
        : base(name, placement, label, cssid, glyph, reverse, speakPhrase ?? Phrases.Conversation_Sent(name))
    {
        Data = data;
    }

    public string Data { get; }
}
