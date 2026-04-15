namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// Implemented by singleton services that must fully initialize before the first scope is
/// created. ApplicationLifecycle awaits all registrations before calling InvokeInScopeAsync.
/// Not re-awaited on scope recycles.
/// </summary>
internal interface IPreScopeInitializer
{
    /// <summary>
    /// Waits for the service to be ready before scope creation can proceed.
    /// </summary>
    /// <param name="activity">Lifecycle activity for reporting progress and errors</param>
    /// <param name="ct">Cancellation token</param>
    Task WaitAsync(ILifecycleActivity activity, CancellationToken ct);
}
