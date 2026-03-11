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
        CancellationTokenSource linkedCts;

        lock (_lockObject)
        {
            scopeTask = _scopeTcs.Task;
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_stopTokenSource.Token, cancellationToken);
        }

        using (linkedCts)
        {
            IApplicationScope scope = await scopeTask.WaitAsync(linkedCts.Token);

            Task invokeTask = scope.InvokeInScopeAsync(workItem, linkedCts.Token);

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
    }

    async Task IApplicationScopeProvider.RecycleScopeAsync()
    {
        if (TryGetCurrentScope(out IApplicationScope? scope))
        {
            await ReleaseScopeAsync(scope);

            await scope.RecycleAsync();
        }
    }

    async Task IApplicationScopeContainer.ReleaseScopeAsync(IApplicationScope scope)
    {
        await ReleaseScopeAsync(scope);
    }

    Task IApplicationScopeContainer.SetScopeAsync(IApplicationScope scope)
    {
        lock (_lockObject)
        {
            if (TryGetCurrentScope(out IApplicationScope? currentScope))
            {
                _ = ReleaseScopeAsync(currentScope);
            }

            _scopeTcs.SetResult(scope);
        }

        return Task.CompletedTask;
    }

    private async Task ReleaseScopeAsync(IApplicationScope scope)
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
