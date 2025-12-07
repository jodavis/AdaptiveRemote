namespace AdaptiveRemote.Services.Lifecycle;

public interface IApplicationScope
{
    Task InvokeInScopeAsync(Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken);

    Task RecycleAsync(CancellationToken cancellationToken);
}
