using System.Diagnostics.CodeAnalysis;

namespace AdaptiveRemote.Services.Lifecycle;

internal class ApplicationScopeContainer : IApplicationScopeContainer, IApplicationScopeProvider
{
    private TaskCompletionSource<IApplicationScope> _scopeTcs = new();
    private List<Task> _invokeTasks = new();

    async Task IApplicationScopeProvider.InvokeInScopeAsync(Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken)
    {
        IApplicationScope scope = await _scopeTcs.Task;

        Task invokeTask = scope.InvokeInScopeAsync(workItem, cancellationToken);

        try
        {
            lock (_invokeTasks)
            {
                _invokeTasks.Add(invokeTask);
            }

            await invokeTask;
        }
        finally
        {
            lock (_invokeTasks)
            {
                _invokeTasks.Remove(invokeTask);
            }
        }
    }

    async Task IApplicationScopeProvider.RecycleScopeAsync(CancellationToken cancellationToken)
    {
        if (TryGetCurrentScope(out IApplicationScope? scope))
        {
            await ReleaseScope();

            await scope.RecycleAsync(cancellationToken);
        }
    }

    async Task IApplicationScopeContainer.ReleaseScopeAsync(IApplicationScope scope)
    {
        if (TryGetCurrentScope(out IApplicationScope? currentScope) &&
            ReferenceEquals(currentScope, scope))
        {
            await ReleaseScope();
        }
    }

    // Explicit implementation for interface - keep existing SetScope for convenience in tests
    async Task IApplicationScopeContainer.SetScopeAsync(IApplicationScope scope)
    {
        SetScope(scope);
        await Task.CompletedTask;
    }

    public void SetScope(IApplicationScope scope)
    {
        // TODO: thread safety?
        if (!_scopeTcs.TrySetResult(scope))
        {
            // TODO: Should throw exception?
            _ = ReleaseScope();
            _scopeTcs.TrySetResult(scope);
        }
    }

    private async Task ReleaseScope()
    {
        List<Task> invokeTasks;
        lock (_invokeTasks)
        {
            invokeTasks = _invokeTasks.ToList();
            _invokeTasks.Clear();
        }

        _scopeTcs = new TaskCompletionSource<IApplicationScope>();

        await Task.WhenAll(invokeTasks);
    }

    private bool TryGetCurrentScope([NotNullWhen(true)] out IApplicationScope? scope)
    {
        if (_scopeTcs.Task.IsCompleted)
        {
            scope = _scopeTcs.Task.Result;
            return true;
        }

        scope = null;
        return false;
    }
}
