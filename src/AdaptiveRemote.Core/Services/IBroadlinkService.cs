namespace AdaptiveRemote.Services;

internal interface IBroadlinkService
{
    Task SendAsync(CancellationToken cancellationToken);
}
