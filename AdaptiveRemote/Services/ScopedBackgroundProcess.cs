using AdaptiveRemote.Logging;
using AdaptiveRemote.Utilities;
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

    protected virtual Task ExecuteOnThreadAsync(Func<CancellationToken, Task> task, CancellationToken cancellationToken)
        => Task.Run(() => task(cancellationToken), cancellationToken);

    public virtual Task InitializeAsync(CancellationToken cancellationToken)
    {
        // TODO: If thread execution is not stopped by the cancellationToken, then the
        // result task should not return cancelled until thread execution is really cancelled
        TaskCompletionSource startTcs = new TaskCompletionSource();

        // TODO: Don't start if cancellation already requested

        // TODO: If cancellation is started before ScopedBackgroundProcess_Started event,
        // cancel ExecuteAsync; after that event, let it run

        Logger.LogInformation(Message.ScopedBackgroundProcess_Starting);

        ExecuteTask = ExecuteOnThreadAsync(ExecuteInternalAsync, _stopToken.Token);
        return startTcs.Task;

        async Task ExecuteInternalAsync(CancellationToken stopToken)
        {
            Task executeTask = ExecuteAsync(stopToken);
            if (executeTask.IsFaulted)
            {
                startTcs.SetException(executeTask.Exception.InnerException!);
                await executeTask;
            }
            else
            {
                startTcs.SetResult();
                Logger.LogInformation(Message.ScopedBackgroundProcess_Started);
            }

            try
            {
                await executeTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!stopToken.IsCancellationRequested)
                {
                    // TODO: Don't log warning if stopToken is set
                    Logger.LogWarning(Message.ScopedBackgroundProcess_StoppedEarly);
                    throw;
                }
            }
            catch (Exception error)
            {
                Logger.LogError(Message.ScopedBackgroundProcess_Error, error);
                throw;
            }
        }
        //TaskCompletionSource startTcs = new();
        //cancellationToken.Register(() => startTcs.TrySetCanceled());

        //if (!cancellationToken.IsCancellationRequested)
        //{
        //    Logger.LogInformation(Message.ScopedBackgroundProcess_Starting);
        //    ExecuteTask = Task.Run(() => ExecuteInternalAsync(_stopToken.Token));
        //}

        //return startTcs.Task;

        //async Task ExecuteInternalAsync(CancellationToken stopToken)
        //{
        //    try
        //    {
        //        Task executeTask = ExecuteAsync(stopToken);

        //        startTcs.TrySetResult();

        //        if (!executeTask.IsCompleted)
        //        {
        //            Logger.LogInformation(Message.ScopedBackgroundProcess_Started);
        //            await executeTask;
        //        }

        //        if (executeTask.IsFaulted)
        //        {
        //            await executeTask;
        //        }

        //        if (!stopToken.IsCancellationRequested)
        //        {
        //            Logger.LogWarning(Message.ScopedBackgroundProcess_StoppedEarly);
        //        }
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        if (!stopToken.IsCancellationRequested)
        //        {
        //            Logger.LogWarning(Message.ScopedBackgroundProcess_StoppedEarly);
        //            throw;
        //        }
        //        startTcs.TrySetCanceled();
        //    }
        //    catch (Exception error)
        //    {
        //        Logger.LogError(Message.ScopedBackgroundProcess_Error, error);
        //        startTcs.TrySetException(error);
        //        throw;
        //    }
        //}
    }

    public virtual async Task CleanUpAsync(CancellationToken cancellationToken)
    {
        // TODO: What if ExecuteTask is not started? (is null?)
        // TODO: What if Initialize is called after Cleanup
        Logger.LogInformation(Message.ScopedBackgroundProcess_Stopping);

        _stopToken.Cancel();
        await Task.WhenAny(ExecuteTask!);

        Logger.LogInformation(Message.ScopedBackgroundProcess_Stopped);
    }
}
