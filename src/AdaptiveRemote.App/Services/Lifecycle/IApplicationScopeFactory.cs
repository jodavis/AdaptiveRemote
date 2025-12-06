namespace AdaptiveRemote.Services.Lifecycle;

public interface IApplicationScopeFactory
{
    Task<IApplicationScope> CreateNewScopeAsync(CancellationToken cancellationToken);
}
