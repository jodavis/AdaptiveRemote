using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services;

public interface IRemoteDefinitionService
{
    RemoteLayoutElement RemoteRoot { get; }
}
