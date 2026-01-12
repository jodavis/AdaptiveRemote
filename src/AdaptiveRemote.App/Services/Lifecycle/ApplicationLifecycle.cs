using AdaptiveRemote.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

internal class ApplicationLifecycle : BackgroundService
{
    private readonly IApplicationScopeProvider _scopeProvider;
    private readonly ILifecycleViewController _viewController;
    private readonly ILogger<ApplicationLifecycle> _logger;
    private ScopedLifecycleContainer? _currentContainer;

    public ApplicationLifecycle(IApplicationScopeProvider scopeProvider, ILifecycleViewController viewController, ILogger<ApplicationLifecycle> logger)
    {
        _scopeProvider = scopeProvider;
        _viewController = viewController;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _scopeProvider.InvokeInScopeAsync(InitializeLifecycleAsync, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Do nothing, shutdown was requested
        }
        catch
        {
            // An error occurred, so stop all the services
            _ = _scopeProvider.InvokeInScopeAsync(CleanUpLifecycleAsync, default);
        }

        await stoppingToken.WaitForCancelledAsync();

        _logger.LogInformation(Message.ApplicationLifecycle_ShuttingDown);

        await _scopeProvider.InvokeInScopeAsync(CleanUpLifecycleAsync, default);
    }

    private async Task InitializeLifecycleAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        _currentContainer = SafeGetContainer(provider);

        if (_currentContainer is not null)
        {
            await _currentContainer.InitializeAllAsync(cancellationToken);
        }

        ScopedLifecycleContainer? SafeGetContainer(IServiceProvider provider)
        {
            try
            {
                return provider.GetRequiredService<ScopedLifecycleContainer>();
            }
            catch (Exception ex)
            {
                _logger.LogError(Message.ApplicationLifecycle_ScopeConstructionFailed, ex);
                _viewController.SetFatalError(ex);
                return null;
            }
        }
    }

    private async Task CleanUpLifecycleAsync(IServiceProvider provider, CancellationToken token)
    {
        ScopedLifecycleContainer? scope = Interlocked.Exchange(ref _currentContainer, null);

        if (scope != null)
        {
            await scope.CleanUpInitializedServicesAsync(token);
        }
    }
}
