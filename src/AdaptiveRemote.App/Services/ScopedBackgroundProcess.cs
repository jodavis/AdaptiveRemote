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

    private Task StartExecutingWithInitializationCancellationAsync(CancellationToken stopToken, CancellationToken cancellationToken)
    {
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(stopToken);

        using (cancellationToken.Register(linked.Cancel))
        {
            return ExecuteAsync(linked.Token);
        }
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
