namespace AdaptiveRemote.Services.Lifecycle;

internal interface IApplicationScopeFactory
{
    Task<IApplicationScope> CreateNewScopeAsync(CancellationToken cancellationToken);
}
