using AdaptiveRemote.Models;
using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Services.Commands;

// Tracks IsActive for a single Command; created by CommandExecutionIdleAdapter.
internal sealed class CommandActivityDetector : MvvmPropertyActivityDetector
{
    internal CommandActivityDetector(Command command)
        : base(command, Command.IsActiveProperty)
    {
    }
}
