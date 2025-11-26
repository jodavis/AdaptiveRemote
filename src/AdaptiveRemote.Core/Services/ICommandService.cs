using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services;

internal interface ICommandService
{
    Task ExecuteAsync(Command command, CancellationToken cancellationToken = default);
}
