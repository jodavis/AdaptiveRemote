using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services;

public interface IRemoteDefinitionService
{
    IEnumerable<string> RemoteNames { get; }
    RemoteLayoutElement GetRoot(string remoteName);
    IEnumerable<Command> GetCommands(string remoteName);
}
