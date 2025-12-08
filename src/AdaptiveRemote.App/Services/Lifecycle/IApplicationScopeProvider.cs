namespace AdaptiveRemote.Services.Lifecycle;

internal interface IApplicationScopeProvider
{
    /// <summary>
    /// Runs the given <paramref name="workItem"/> using a scoped IServiceProvider.
    /// </summary>
    /// <returns>A Task that completes when the work item completes</returns>
    Task InvokeInScopeAsync(Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken);

    /// <summary>
    /// Resets the current IApplicationScope. Subsequent calls to <see cref="InvokeInScopeAsync"/>
    /// will be delayed until a new IApplicationScope is available.
    /// </summary>
    /// <returns>A Task that completes when the scope has been reset.</returns>
    Task RecycleScopeAsync();
}
