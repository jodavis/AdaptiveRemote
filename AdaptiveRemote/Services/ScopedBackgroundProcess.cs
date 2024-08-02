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

    public virtual Task InitializeAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource startTcs = new();
        cancellationToken.Register(() => startTcs.TrySetCanceled());

        if (!cancellationToken.IsCancellationRequested)
        {
            Logger.LogInformation(Message.ScopedBackgroundProcess_Starting);
            ExecuteTask = Task.Run(() => ExecuteInternalAsync(_stopToken.Token), cancellationToken);
        }

        return startTcs.Task;

        async Task ExecuteInternalAsync(CancellationToken stopToken)
        {
            try
            {
                Task executeTask = ExecuteAsync(stopToken);

                startTcs.TrySetResult();

                if (!executeTask.IsCompleted)
                {
                    Logger.LogInformation(Message.ScopedBackgroundProcess_Started);
                    await executeTask;
                }

                if (executeTask.IsFaulted)
                {
                    await executeTask;
                }

                if (!stopToken.IsCancellationRequested)
                {
                    Logger.LogWarning(Message.ScopedBackgroundProcess_StoppedEarly);
                }
            }
            catch (OperationCanceledException)
            {
                if (!stopToken.IsCancellationRequested)
                {
                    Logger.LogWarning(Message.ScopedBackgroundProcess_StoppedEarly);
                    throw;
                }
                startTcs.TrySetCanceled(stopToken);
            }
            catch (Exception error)
            {
                Logger.LogError(Message.ScopedBackgroundProcess_Error, error);
                startTcs.TrySetException(error);
                throw;
            }
        }
    }

    public virtual async Task CleanUpAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation(Message.ScopedBackgroundProcess_Stopping);

        _stopToken.Cancel();
        await (ExecuteTask ?? Task.CompletedTask).CancelWaitingOn(cancellationToken);

        Logger.LogInformation(Message.ScopedBackgroundProcess_Stopped);
    }
}
