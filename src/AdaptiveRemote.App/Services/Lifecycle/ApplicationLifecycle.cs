using AdaptiveRemote.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

internal class ApplicationLifecycle : BackgroundService
{
    private readonly IApplicationScopeProvider _scopeProvider;
    private readonly ILifecycleViewController _viewController;
    private readonly IApplicationRecycleSignal _signal;
    private readonly IEnumerable<IPreScopeInitializer> _preInitializers;
    private readonly MessageLogger _logger;
    private ScopedLifecycleContainer? _currentContainer;

    public ApplicationLifecycle(
        IApplicationScopeProvider scopeProvider,
        ILifecycleViewController viewController,
        IApplicationRecycleSignal signal,
        IEnumerable<IPreScopeInitializer> preInitializers,
        ILogger<ApplicationLifecycle> logger)
    {
        _scopeProvider = scopeProvider;
        _viewController = viewController;
        _signal = signal;
        _preInitializers = preInitializers;
        _logger = new(logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Await all IPreScopeInitializer services before creating the first scope.
            // Not re-awaited on scope recycles — the store is already populated.
            await RunPreInitializersAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                using CancellationTokenSource linkedCts =
                    CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _signal.Token);
                bool initCompleted = false;

                _logger.ApplicationLifecycle_WaitingForScope();

                try
                {
                    await _scopeProvider.InvokeInScopeAsync(async (provider, ct) =>
                    {
                        bool success = await TryInitializeScopeAsync(provider, ct);
                        if (!success) return;

                        initCompleted = true;
                        _logger.ApplicationLifecycle_ScopeReleased();

                        // Steady-state: block until stoppingToken or signal.Token fires.
                        // Task.Delay throws OperationCanceledException on cancellation,
                        // which propagates out to the appropriate catch clause in ExecuteAsync.
                        await Task.Delay(Timeout.Infinite, ct);
                    }, linkedCts.Token);

                    // Work item returned normally: init failed internally (non-OCE) and
                    // cleaned up; log ScopeReleased and exit the loop.
                    _logger.ApplicationLifecycle_ScopeReleased();
                    break;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break; // Normal shutdown
                }
                catch (OperationCanceledException)
                {
                    // Recycle signal fired — fall through to recycle logic below
                }

                // Recycle path
                await CleanUpCurrentContainerAsync(default);

                if (initCompleted)
                {
                    // Signal fired during steady state: recycle the scope (triggers browser reload).
                    _logger.ApplicationLifecycle_RecyclingScope();
                    await _scopeProvider.RecycleScopeAsync();
                }
                // else: signal fired during init — no RecycleScopeAsync; the existing scope
                // TCS is still valid, so the next InvokeInScopeAsync re-enters the same scope.

                _signal.Reset();
            }
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

    private async Task RunPreInitializersAsync(CancellationToken stoppingToken)
    {
        Task[] initTasks = _preInitializers.Select(init => RunSinglePreInitializerAsync(init, stoppingToken)).ToArray();
        await Task.WhenAll(initTasks);
    }

    private async Task RunSinglePreInitializerAsync(IPreScopeInitializer initializer, CancellationToken stoppingToken)
    {
        ILifecycleActivity activity = _viewController.StartTask($"Initializing {initializer.GetType().Name}");
        try
        {
            await initializer.WaitAsync(activity, stoppingToken);
        }
        finally
        {
            activity.Dispose();
        }
    }

    private async Task<bool> TryInitializeScopeAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        _currentContainer = SafeGetContainer(provider);

        if (_currentContainer is null) return false;

        try
        {
            await _currentContainer.InitializeAllAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Service initialization failures are already logged in ScopedLifecycleContainer.
            await CleanUpCurrentContainerAsync(default);
            return false;
        }
    }

    private ScopedLifecycleContainer? SafeGetContainer(IServiceProvider provider)
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

    private async Task CleanUpCurrentContainerAsync(CancellationToken cancellationToken)
    {
        ScopedLifecycleContainer? scope = Interlocked.Exchange(ref _currentContainer, null);

        if (scope != null)
        {
            await scope.CleanUpInitializedServicesAsync(cancellationToken);
        }
    }
}
