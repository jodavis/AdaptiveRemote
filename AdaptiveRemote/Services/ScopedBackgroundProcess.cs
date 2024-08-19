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

    protected virtual Task ExecuteOnThreadAsync(Func<Task> task, CancellationToken cancellationToken)
        => Task.Run(() => task(), cancellationToken);

    public virtual Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        Logger.LogInformation(Message.ScopedBackgroundProcess_Starting);

        TaskCompletionSource startTcs = new();

        ExecuteTask = ExecuteOnThreadAsync(() => StartProcess(cancellationToken), cancellationToken);
        ExecuteTask.ContinueWith(t =>
        {
            if (startTcs.TrySetCanceled())
            {
                Logger.LogWarning(Message.ScopedBackgroundProcess_CanceledBeforeStarted);
            }
        }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnCanceled);

        return startTcs.Task;

        async Task StartProcess(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(_stopToken.Token);

                // TODO: If cancellationToken is called before this returns, ExecuteAsync should be cancelled
                // But if cancellationToken is triggered after this returns, ExecuteAsync should not be cancelled
                Task task;
                using (cancellationToken.Register(linked.Cancel))
                {
                    task = ExecuteAsync(linked.Token);
                }

                if (!task.IsFaulted && !task.IsCanceled && startTcs.TrySetResult())
                {
                    Logger.LogInformation(Message.ScopedBackgroundProcess_Started);
                }

                await task;

                if (!_stopToken.IsCancellationRequested && !startTcs.TrySetCanceled(CancellationToken.None))
                {
                    Logger.LogWarning(Message.ScopedBackgroundProcess_StoppedEarly);
                }
            }
            catch (OperationCanceledException)
            {
                if (startTcs.TrySetCanceled(CancellationToken.None))
                {
                    Logger.LogWarning(Message.ScopedBackgroundProcess_CanceledBeforeStarted);
                    throw;
                }
                else if (!_stopToken.IsCancellationRequested)
                {
                    Logger.LogWarning(Message.ScopedBackgroundProcess_StoppedEarly);
                    throw;
                }
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
    }

    public virtual async Task CleanUpAsync(CancellationToken cancellationToken)
    {
        if (ExecuteTask?.IsCompleted == false)
        {
            Logger.LogInformation(Message.ScopedBackgroundProcess_Stopping);

            _stopToken.Cancel();
            //// TODO: What if ExecuteTask is not started? (is null?)
            //// TODO: What if Initialize is called after Cleanup
            await Task.WhenAny(ExecuteTask!);

            Logger.LogInformation(Message.ScopedBackgroundProcess_Stopped);
        }
    }
}
