using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// ScopedLifecycleContainer is a scoped service that imports all the <see cref="IScopedLifecycle">
/// services and manages calls to InitializeAsync and CleanUpAsync for the lifecycle of a scope.
/// </summary>
internal class ScopedLifecycleContainer
{
    private readonly IEnumerable<IScopedLifecycle> _services;
    private readonly ILifecycleViewController _controller;
    private readonly ILogger<ScopedLifecycleContainer> _logger;

    public ScopedLifecycleContainer(IEnumerable<IScopedLifecycle> services, ILifecycleViewController controller, ILogger<ScopedLifecycleContainer> logger)
    {
        _services = services;
        _controller = controller;
        _logger = logger;
    }

    public async Task InitializeAllAsync(CancellationToken cancellationToken)
    {
        List<IScopedLifecycle> initializedServices = new();
        List<Task> initializeTasks = new();
        using CancellationTokenSource abortTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellationToken = abortTokenSource.Token;

        _controller.SetPhase(LifecyclePhase.SettingUp);

        foreach (IScopedLifecycle scopedService in _services)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // do not start further services if cancellation already requested
                break;
            }

            initializedServices.Add(scopedService);

            initializeTasks.Add(InitializeServiceAsync(scopedService, cancellationToken));

            if (initializeTasks.Any(x => x.IsFaulted))
            {
                abortTokenSource.Cancel();
                break;
            }
        }

        try
        {
            await Task.WhenAll(initializeTasks);

            cancellationToken.ThrowIfCancellationRequested();

            _controller.SetPhase(LifecyclePhase.Ready);
        }
        catch
        {
            _ = CleanUpAllAsync(initializedServices, default);

            throw;
        }
    }

    private async Task InitializeServiceAsync(IScopedLifecycle scopedService, CancellationToken cancellationToken)
    {
        _logger.LogInformation(Message.ApplicationLifecycle_Initializing, scopedService.Name);
        using ILifecycleActivity activity = _controller.StartTask(Phrases.Startup_Starting(scopedService.Name));
        try
        {

            await scopedService.InitializeAsync(activity, cancellationToken);
            _logger.LogInformation(Message.ApplicationLifecycle_Initialized, scopedService.Name);
        }
        catch (OperationCanceledException)
        { }
        catch (Exception error)
        {
            _logger.LogError(Message.ApplicationLifecycle_InitializingFailed, scopedService.Name, error);
            activity.SetFatalError(error);
            throw;
        }
    }

    public Task CleanUpAllAsync(CancellationToken cancellationToken)
        => CleanUpAllAsync(_services, cancellationToken);

    private async Task CleanUpAllAsync(IEnumerable<IScopedLifecycle> scopedServices, CancellationToken cancellationToken)
    {
        _controller.SetPhase(LifecyclePhase.CleaningUp);

        List<Task> cleanUpTasks = new();

        foreach (IScopedLifecycle scopedService in scopedServices)
        {
            cleanUpTasks.Add(CleanUpServiceAsync(scopedService, cancellationToken));
        }

        await Task.WhenAll(cleanUpTasks);
    }

    private async Task CleanUpServiceAsync(IScopedLifecycle scopedService, CancellationToken cancellationToken)
    {
        _logger.LogInformation(Message.ApplicationLifecycle_CleaningUp, scopedService.Name);
        using ILifecycleActivity activity = _controller.StartTask(Phrases.Cleanup_CleaningUp(scopedService.Name));
        try
        {
            await scopedService.CleanUpAsync(activity, cancellationToken);
            _logger.LogInformation(Message.ApplicationLifecycle_CleanedUp, scopedService.Name);
        }
        catch (Exception error)
        {
            activity.SetFatalError(error);
            _logger.LogError(Message.ApplicationLifecycle_CleaningUpFailed, scopedService.Name, error);
        }
    }
}
