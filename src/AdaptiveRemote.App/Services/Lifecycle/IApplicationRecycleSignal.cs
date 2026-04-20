namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// Signals that a scope recycle has been requested. ApplicationLifecycle links this token
/// into its scope work item; RequestRecycle() cancels that token whether init is in progress
/// or the loop is in steady-state wait. Reset() is called by ApplicationLifecycle after
/// cleanup, before starting the next init cycle.
/// </summary>
internal interface IApplicationRecycleSignal
{
    void RequestRecycle();
    CancellationToken Token { get; }
    void Reset();
}
