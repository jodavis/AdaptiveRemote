
namespace AdaptiveRemote.Services.TiVo;

internal interface ITiVoConnection
{
    Task SendAsync(string commandId, CancellationToken cancellationToken);

    Task DisposeAsync(CancellationToken cancellationToken);
}
