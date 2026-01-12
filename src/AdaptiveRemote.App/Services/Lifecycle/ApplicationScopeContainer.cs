using System.Diagnostics.CodeAnalysis;

namespace AdaptiveRemote.Services.Lifecycle;

internal class ApplicationScopeContainer : IApplicationScopeContainer, IApplicationScopeProvider
{
    private TaskCompletionSource<IApplicationScope> _scopeTcs = new();
    private CancellationTokenSource _stopTokenSource = new();
    private List<Task> _invokeTasks = new();
    private readonly object _lockObject = new();

    async Task IApplicationScopeProvider.InvokeInScopeAsync(Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken)
    {
        Task<IApplicationScope> scopeTask;
        CancellationToken combinedToken;

        lock (_lockObject)
        {
            scopeTask = _scopeTcs.Task;
            combinedToken = CancellationTokenSource.CreateLinkedTokenSource(_stopTokenSource.Token, cancellationToken).Token;
        }

        IApplicationScope scope = await scopeTask;

        Task invokeTask = scope.InvokeInScopeAsync(workItem, combinedToken);

        if (!invokeTask.IsCompletedSuccessfully)
        {
            try
            {
                lock (_lockObject)
                {
                    _invokeTasks.Add(invokeTask);
                }

                await invokeTask;
            }
            finally
            {
                lock (_lockObject)
                {
                    _invokeTasks.Remove(invokeTask);
                }
            }
        }
    }

    async Task IApplicationScopeProvider.RecycleScopeAsync()
    {
        if (TryGetCurrentScope(out IApplicationScope? scope))
        {
            await ReleaseScope(scope);

            await scope.RecycleAsync();
        }
    }

    async Task IApplicationScopeContainer.ReleaseScopeAsync(IApplicationScope scope)
    {
        await ReleaseScope(scope);
    }

    Task IApplicationScopeContainer.SetScopeAsync(IApplicationScope scope)
    {
        lock (_lockObject)
        {
            if (TryGetCurrentScope(out IApplicationScope? currentScope))
            {
                _ = ReleaseScope(currentScope);
            }

            _scopeTcs.SetResult(scope);
        }

        return Task.CompletedTask;
    }

    private async Task ReleaseScope(IApplicationScope scope)
    {
        IEnumerable<Task> tasksToAwait = Enumerable.Empty<Task>();

        lock (_lockObject)
        {
            if (TryGetCurrentScope(out IApplicationScope? currentScope) &&
                ReferenceEquals(currentScope, scope))
            {
                _invokeTasks.Add(_stopTokenSource.CancelAsync());
                _stopTokenSource = new CancellationTokenSource();

                tasksToAwait = _invokeTasks;
                _invokeTasks = new();

                _scopeTcs = new TaskCompletionSource<IApplicationScope>();
            }
        }

        await Task.WhenAll(tasksToAwait);
    }

    private bool TryGetCurrentScope([NotNullWhen(true)] out IApplicationScope? scope)
        => _scopeTcs.Task.TryGetResultIfComplete(out scope);
}
