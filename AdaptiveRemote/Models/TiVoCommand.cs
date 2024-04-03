namespace AdaptiveRemote.Models;

public class TiVoCommand : Command
{
    public TiVoCommand(string group, string label)
        : this(group, label, label.ToUpperInvariant())
    { }

    public TiVoCommand(string group, string label, string commandId)
        : base(group, label, commandId)
    { }
}
