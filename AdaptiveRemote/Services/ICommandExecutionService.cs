using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services;

public interface ICommandExecutionService
{
    public Task ExecuteAsync(Command command, CancellationToken cancellationToken = default);
}
