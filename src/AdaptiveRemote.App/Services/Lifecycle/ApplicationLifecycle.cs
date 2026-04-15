using AdaptiveRemote.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

internal class ApplicationLifecycle : BackgroundService
{
    private readonly IApplicationScopeProvider _scopeProvider;
    private readonly ILifecycleViewController _viewController;
    private readonly IEnumerable<IPreScopeInitializer> _preInitializers;
    private readonly MessageLogger _logger;
    private ScopedLifecycleContainer? _currentContainer;

    public ApplicationLifecycle(
        IApplicationScopeProvider scopeProvider,
        ILifecycleViewController viewController,
        IEnumerable<IPreScopeInitializer> preInitializers,
        ILogger<ApplicationLifecycle> logger)
    {
        _scopeProvider = scopeProvider;
        _viewController = viewController;
        _preInitializers = preInitializers;
        _logger = new(logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Await all IPreScopeInitializer services before creating the first scope
            Task[] initTasks = _preInitializers.Select(init =>
            {
                ILifecycleActivity activity = _viewController.StartTask($"Initializing {init.GetType().Name}");
                return init.WaitAsync(activity, stoppingToken);
            }).ToArray();
            await Task.WhenAll(initTasks);

            _logger.ApplicationLifecycle_WaitingForScope();

            await _scopeProvider.InvokeInScopeAsync(InitializeLifecycleAsync, stoppingToken);
            _logger.ApplicationLifecycle_ScopeReleased();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Do nothing, shutdown was requested
        }
        catch (Exception ex)
        {
            _logger.ApplicationLifecycle_UnhandledError(ex);
            await CleanUpCurrentContainerAsync(default);
        }

        try
        {
            await stoppingToken.WaitForCancelledAsync();
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }

        _logger.ApplicationLifecycle_ShuttingDown();

        await CleanUpCurrentContainerAsync(default);
    }

    private async Task InitializeLifecycleAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        _currentContainer = SafeGetContainer(provider);

        if (_currentContainer is not null)
        {
            try
            {
                await _currentContainer.InitializeAllAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Service initialization failures are already logged in ScopedLifecycleContainer.
                // Clean up and return normally so ExecuteAsync can log ScopeReleased.
                await CleanUpCurrentContainerAsync(default);
            }
        }

        ScopedLifecycleContainer? SafeGetContainer(IServiceProvider provider)
        {
            try
            {
                return provider.GetRequiredService<ScopedLifecycleContainer>();
            }
            catch (Exception ex)
            {
                _logger.ApplicationLifecycle_ScopeConstructionFailed(ex);
                _viewController.SetFatalError(ex);
                return null;
            }
        }
    }

    private async Task CleanUpCurrentContainerAsync(CancellationToken cancellationToken)
    {
        ScopedLifecycleContainer? scope = Interlocked.Exchange(ref _currentContainer, null);

        if (scope != null)
        {
            await scope.CleanUpInitializedServicesAsync(cancellationToken);
        }
    }
}
