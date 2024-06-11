namespace AdaptiveRemote.Services.TiVo;

internal class PlaceholderTiVoService : ITiVoService
{
    Task ITiVoService.SendAsync(string commandCode, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
