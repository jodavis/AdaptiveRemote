using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services;

internal abstract class ScopedBackgroundProcess : IScopedLifecycle
{
    private readonly CancellationTokenSource _stopToken = new();

    protected ScopedBackgroundProcess(string name, ILogger logger)
    {
        Name = name;
        Logger = new(logger);
    }

    public string Name { get; }
    protected MessageLogger Logger { get; }
    public Task? ExecuteTask { get; private set; }

    protected abstract Task ExecuteAsync(CancellationToken stopToken);

    protected virtual Task MoveToWorkerThreadAsync(Func<Task> task, CancellationToken cancellationToken)
    {
        Logger.ScopedBackgroundProcess_SwitchingToWorkerThread();
        return Task.Run(() =>
        {
            Logger.ScopedBackgroundProcess_SwitchedToWorkerThread(Environment.CurrentManagedThreadId);
            return task();
        }, cancellationToken);
    }

    public virtual Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        TaskCompletionSource startTcs = new();

        ExecuteTask = StartExecutingWithErrorHandlingAsync(startTcs, _stopToken.Token, cancellationToken);

        return startTcs.Task;
    }

    private async Task StartExecutingWithErrorHandlingAsync(TaskCompletionSource startTcs, CancellationToken stopToken, CancellationToken initializationToken)
    {
        try
        {
            initializationToken.ThrowIfCancellationRequested();

            Logger.ScopedBackgroundProcess_Starting();

            await MoveToWorkerThreadAsync(() => StartExecutingOnWorkerThreadAsync(startTcs, stopToken, initializationToken), initializationToken);

            if (!stopToken.IsCancellationRequested)
            {
                Logger.ScopedBackgroundProcess_StoppedEarly();
            }
        }
        catch (OperationCanceledException)
        {
            if (startTcs.TrySetCanceled(CancellationToken.None))
            {
                Logger.ScopedBackgroundProcess_CanceledBeforeStarted();
            }
            else if (stopToken.IsCancellationRequested)
            {
                // Normal stop
                return;
            }
            else
            {
                Logger.ScopedBackgroundProcess_StoppedEarly();
            }
            throw;
        }
        catch (Exception error)
        {
            if (!startTcs.TrySetException(error))
            {
                Logger.ScopedBackgroundProcess_Error(error);
            }
            throw;
        }
    }

    private Task StartExecutingOnWorkerThreadAsync(TaskCompletionSource startTcs, CancellationToken stopToken, CancellationToken initializationToken)
    {
        initializationToken.ThrowIfCancellationRequested();

        Task task = StartExecutingWithInitializationCancellationAsync(stopToken, initializationToken);

        if (!task.IsCompleted && startTcs.TrySetResult())
        {
            Logger.ScopedBackgroundProcess_Started();
        }

        return task;
    }

    // This method is `async Task` (rather than returning the Task from ExecuteAsync directly)
    // so that the `using` block properly disposes `linked` after ExecuteAsync completes.
    // Exception propagation is equivalent: exceptions from ExecuteAsync still propagate to the
    // caller (StartExecutingOnWorkerThreadAsync) as a faulted Task.
    private async Task StartExecutingWithInitializationCancellationAsync(CancellationToken stopToken, CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(stopToken);

        // Apply initializationToken cancellation only during the synchronous startup phase.
        // Once ExecuteAsync has started (returned a Task at its first await), the registration
        // is removed so that further initialization cancellation does not stop the running service.
        Task executeTask;
        using (cancellationToken.Register(linked.Cancel))
        {
            executeTask = ExecuteAsync(linked.Token);
        }

        await executeTask;
    }

    public virtual async Task CleanUpAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        if (ExecuteTask?.IsCompleted == false)
        {
            Logger.ScopedBackgroundProcess_Stopping();

            await _stopToken.CancelAsync();

            await Task.Run(async () =>
            {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign tasks -- we are intentionally switching to the context of ExecuteTask here
                await Task.WhenAny(ExecuteTask);
#pragma warning restore VSTHRD003 // Avoid awaiting foreign tasks
            }, cancellationToken).ConfigureAwait(false);

            Logger.ScopedBackgroundProcess_Stopped();
        }
    }
}
