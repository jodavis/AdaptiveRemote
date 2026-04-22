namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// Signals that a scope recycle has been requested. ApplicationLifecycle links this token
/// into its scope work item; RequestRecycle() cancels that token whether init is in progress
/// or the loop is in steady-state wait. Reset() is called by ApplicationLifecycle after
/// cleanup, before starting the next init cycle.
/// </summary>
internal interface IApplicationRecycleSignal
{
    /// <summary>
    /// Requests a scope recycle. Cancels <see cref="Token"/>.
    /// Safe to call from any thread, including concurrently with <see cref="Reset"/>.
    /// </summary>
    void RequestRecycle();

    /// <summary>
    /// The cancellation token that fires when <see cref="RequestRecycle"/> is called.
    /// Linked into the scope work item by ApplicationLifecycle.
    /// </summary>
    CancellationToken Token { get; }

    /// <summary>
    /// Resets the signal after cleanup, replacing the cancelled token with a fresh one.
    /// Called by ApplicationLifecycle before starting the next scope iteration.
    /// </summary>
    void Reset();
}
