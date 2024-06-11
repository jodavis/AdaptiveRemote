namespace AdaptiveRemote.Services;

internal interface ITiVoService
{
    Task SendAsync(string commandCode, CancellationToken cancellationToken);
}
