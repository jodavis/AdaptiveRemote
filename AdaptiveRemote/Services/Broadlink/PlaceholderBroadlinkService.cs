namespace AdaptiveRemote.Services.Broadlink;

internal class PlaceholderBroadlinkService : IBroadlinkService
{
    Task IBroadlinkService.SendAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
