using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Commands;

// Tracks IsActive for a single Command; created by CommandExecutionIdleAdapter.
internal sealed class CommandIdleAdapter : MvvmPropertyIdleAdapter
{
    internal CommandIdleAdapter(Command command)
        : base(command, Command.IsActiveProperty)
    {
    }
}
