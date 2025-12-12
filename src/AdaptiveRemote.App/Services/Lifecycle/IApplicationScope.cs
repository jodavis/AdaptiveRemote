namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// Represents an application-level scoped service container that can execute work
/// within a scoped <see cref="IServiceProvider"/> and can be recycled (replaced)
/// to refresh underlying scoped services.
/// </summary>
public interface IApplicationScope
{
    /// <summary>
    /// Executes an asynchronous work item within the application-managed scope.
    /// </summary>
    /// <param name="workItem">
    /// A delegate that receives an <see cref="IServiceProvider"/> (rooted at the managed scope)
    /// and a <see cref="CancellationToken"/> and performs asynchronous work. The delegate
    /// should not assume it runs on a specific thread and should respect the provided token.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the request to invoke the work item. The token
    /// may be observed by the implementation to avoid starting the delegate if cancellation
    /// is requested before invocation.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes when the <paramref name="workItem"/> has finished executing.</returns>
    /// <remarks>
    /// Exceptions thrown by the <paramref name="workItem"/> propagate to the returned task.
    /// The provided <see cref="IServiceProvider"/> is valid only for the duration of the
    /// delegate invocation; do not capture and use it after the delegate completes.
    /// </remarks>
    Task InvokeInScopeAsync(Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken);

    /// <summary>
    /// Recycles the underlying application scope asynchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the recycle operation has finished.</returns>
    Task RecycleAsync();
}
