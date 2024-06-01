
namespace AdaptiveRemote.Services.Commands;

internal interface IApplicationService
{
    Task ExecuteCommandAsync(string name, CancellationToken cancellationToken);
}
