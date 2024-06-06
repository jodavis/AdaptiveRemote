namespace AdaptiveRemote.Services.Lifecycle;

public interface IApplicationScope : IDisposable
{
    Task TryInvokeAsync(Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken);
}
