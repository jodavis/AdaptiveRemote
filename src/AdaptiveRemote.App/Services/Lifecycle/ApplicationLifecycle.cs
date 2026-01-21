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
    private readonly TaskCompletionSource _initializationComplete = new();

    public ApplicationLifecycle(IApplicationScopeProvider scopeProvider, ILifecycleViewController viewController, ILogger<ApplicationLifecycle> logger)
    {
        _scopeProvider = scopeProvider;
        _viewController = viewController;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
        // Wait for initialization to complete before returning
        // This maintains compatibility with .NET 8 behavior where tests expect
        // StartAsync to complete after services are initialized
        try
        {
            await _initializationComplete.Task;
        }
        catch
        {
            // Swallow exceptions from initialization - they're handled in ExecuteAsync
            // We just need to wait for initialization to finish (successfully or not)
        }
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
            _initializationComplete.TrySetResult(); // Still complete initialization tracking
        }
        catch (Exception)
        {
            // An error occurred, so stop all the services
            _ = _scopeProvider.InvokeInScopeAsync(CleanUpLifecycleAsync, default);
            // Note: _initializationComplete is set in InitializeLifecycleAsync
        }

        await stoppingToken.WaitForCancelledAsync();

        _logger.LogInformation(Message.ApplicationLifecycle_ShuttingDown);

        await _scopeProvider.InvokeInScopeAsync(CleanUpLifecycleAsync, default);
    }

    private async Task InitializeLifecycleAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        _currentContainer = SafeGetContainer(provider);

        // Signal that StartAsync can complete now that we've started initialization
        // This maintains compatibility with .NET 8 behavior where StartAsync completes
        // after initialization starts (not necessarily finishes)
        _initializationComplete.TrySetResult();

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
