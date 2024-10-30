using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services;

internal abstract class ScopedBackgroundProcess : IScopedLifecycle
{
    private readonly CancellationTokenSource _stopToken = new();

    protected ScopedBackgroundProcess(string name, ILogger logger)
    {
        Name = name;
        Logger = logger;
    }

    public string Name { get; }
    protected ILogger Logger { get; }
    public Task? ExecuteTask { get; private set; }

    protected abstract Task ExecuteAsync(CancellationToken stopToken);

    protected virtual Task MoveToWorkerThreadAsync(Func<Task> task, CancellationToken cancellationToken)
    {
        Logger.LogDebug(Message.ScopedBackgroundProcess_SwitchingToWorkerThread);
        return Task.Run(() =>
        {
            Logger.LogDebug(Message.ScopedBackgroundProcess_SwitchedToWorkerThread, Environment.CurrentManagedThreadId);
            return task();
        }, cancellationToken);
    }

    public virtual Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        TaskCompletionSource startTcs = new();

        ExecuteTask = StartExecutingWithErrorHandling(startTcs, _stopToken.Token, cancellationToken);

        return startTcs.Task;
    }

    private async Task StartExecutingWithErrorHandling(TaskCompletionSource startTcs, CancellationToken stopToken, CancellationToken initializationToken)
    {
        try
        {
            initializationToken.ThrowIfCancellationRequested();

            Logger.LogInformation(Message.ScopedBackgroundProcess_Starting);

            await MoveToWorkerThreadAsync(() => StartExecutingOnWorkerThread(startTcs, stopToken, initializationToken), initializationToken);

            if (!stopToken.IsCancellationRequested)
            {
                Logger.LogWarning(Message.ScopedBackgroundProcess_StoppedEarly);
            }
        }
        catch (OperationCanceledException)
        {
            if (startTcs.TrySetCanceled(CancellationToken.None))
            {
                Logger.LogWarning(Message.ScopedBackgroundProcess_CanceledBeforeStarted);
            }
            else if (stopToken.IsCancellationRequested)
            {
                // Normal stop
                return;
            }
            else
            {
                Logger.LogWarning(Message.ScopedBackgroundProcess_StoppedEarly);
            }
            throw;
        }
        catch (Exception error)
        {
            if (!startTcs.TrySetException(error))
            {
                Logger.LogError(Message.ScopedBackgroundProcess_Error, error);
            }
            throw;
        }
    }

    private Task StartExecutingOnWorkerThread(TaskCompletionSource startTcs, CancellationToken stopToken, CancellationToken initializationToken)
    {
        initializationToken.ThrowIfCancellationRequested();

        Task task = StartExecutingWithInitializationCancellation(stopToken, initializationToken);

        if (!task.IsCompleted && startTcs.TrySetResult())
        {
            Logger.LogInformation(Message.ScopedBackgroundProcess_Started);
        }

        return task;
    }

    private Task StartExecutingWithInitializationCancellation(CancellationToken stopToken, CancellationToken cancellationToken)
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
            Logger.LogInformation(Message.ScopedBackgroundProcess_Stopping);

            _stopToken.Cancel();

            await Task.WhenAny(ExecuteTask!);

            Logger.LogInformation(Message.ScopedBackgroundProcess_Stopped);
        }
    }
}
