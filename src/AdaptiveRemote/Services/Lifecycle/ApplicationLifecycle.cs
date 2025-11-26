using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

internal class ApplicationLifecycle : BackgroundService
{
    private readonly IApplicationScopeFactory _scopeFactory;
    private readonly ILifecycleViewController _controller;
    private readonly ILogger<ApplicationLifecycle> _logger;
    private readonly List<IScopedLifecycle> _scopedServices = new();

    public ApplicationLifecycle(IApplicationScopeFactory scopeFactory, ILifecycleViewController controller, ILogger<ApplicationLifecycle> logger)
    {
        _scopeFactory = scopeFactory;
        _controller = controller;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IApplicationScope scope = await _scopeFactory.CreateNewScopeAsync(default);

        _ = scope.TryInvokeAsync(InitializeLifecycle, stoppingToken);

        await stoppingToken.WaitForCancelled();

        _logger.LogInformation(Message.ApplicationLifecycle_ShuttingDown);

        await scope.TryInvokeAsync(CleanUpLifecycle, default);
    }

    private async Task InitializeLifecycle(IServiceProvider provider, CancellationToken cancellationToken)
    {
        List<Task> tasks = new();
        CancellationTokenSource abortTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellationToken = abortTokenSource.Token;

        _controller.SetPhase(LifecyclePhase.SettingUp);

        foreach (IScopedLifecycle scopedService in provider.GetServices<IScopedLifecycle>())
        {
            _scopedServices.Add(scopedService);

            tasks.Add(InitializeServiceAsync(scopedService, cancellationToken));

            if (tasks.Any(x => x.IsFaulted))
            {
                abortTokenSource.Cancel();
                break;
            }
        }

        try
        {
            await Task.WhenAll(tasks);

            cancellationToken.ThrowIfCancellationRequested();

            _controller.SetPhase(LifecyclePhase.Ready);
        }
        catch
        {
            _ = CleanUpLifecycle(provider, default);
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

    private async Task CleanUpLifecycle(IServiceProvider provider, CancellationToken cancellationToken)
    {
        _controller.SetPhase(LifecyclePhase.CleaningUp);

        List<Task> cleanUpTasks = new();
        List<IScopedLifecycle> scopedServices = _scopedServices.ToList();
        _scopedServices.Clear();

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
