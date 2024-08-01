using AdaptiveRemote.Logging;
using AdaptiveRemote.Utilities;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services;

internal abstract class ScopedBackgroundProcess : IScopedLifecycle
{
    private readonly CancellationTokenSource _stopToken = new();
    private readonly ILogger _logger;

    protected ScopedBackgroundProcess(string name, ILogger logger)
    {
        Name = name;
        _logger = logger;
    }

    public string Name { get; }
    public Task? ExecuteTask { get; private set; }

    protected abstract Task ExecuteAsync(CancellationToken stopToken);

    public virtual Task InitializeAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource startTcs = new();
        cancellationToken.Register(() => startTcs.TrySetCanceled());

        if (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(Message.ScopedBackgroundProcess_Starting);
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
                    _logger.LogInformation(Message.ScopedBackgroundProcess_Started);
                    await executeTask;
                }

                if (!stopToken.IsCancellationRequested)
                {
                    _logger.LogWarning(Message.ScopedBackgroundProcess_StoppedEarly);
                }
            }
            catch (OperationCanceledException)
            {
                if (!stopToken.IsCancellationRequested)
                {
                    _logger.LogWarning(Message.ScopedBackgroundProcess_StoppedEarly);
                    throw;
                }
                startTcs.TrySetCanceled(stopToken);
            }
            catch (Exception error)
            {
                _logger.LogError(Message.ScopedBackgroundProcess_Error, error);
                startTcs.TrySetException(error);
                throw;
            }
        }
    }

    public virtual async Task CleanUpAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(Message.ScopedBackgroundProcess_Stopping);

        _stopToken.Cancel();
        await (ExecuteTask ?? Task.CompletedTask).CancelWaitingOn(cancellationToken);

        _logger.LogInformation(Message.ScopedBackgroundProcess_Stopped);
    }
}
