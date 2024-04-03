namespace AdaptiveRemote.Models;

public class ExitCommand : Command
{
    public ExitCommand(string group)
        : base(group, "Exit")
    {
    }
}
