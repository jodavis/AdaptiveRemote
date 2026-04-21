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

                _logger.ApplicationLifecycle_WaitingForScope();

                bool initCompleted = await TryInitializeScopeAsync(linkedCts.Token);

                if (!initCompleted && !linkedCts.Token.IsCancellationRequested)
                {
                    // Construction or init failure — already logged; exit the loop.
                    _logger.ApplicationLifecycle_ScopeReleased();
                    break;
                }

                if (initCompleted)
                {
                    // Scope is ready; block until stoppingToken or signal.Token fires.
                    _logger.ApplicationLifecycle_ScopeReady();
                    await linkedCts.Token.WaitForCancelledAsync();
                }

                await CleanUpCurrentContainerAsync(default);

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                if (initCompleted)
                {
                    // Signal fired during steady-state: recycle the scope (triggers browser reload).
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

        await stoppingToken.WaitForCancelledAsync();
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

    private async Task<bool> TryInitializeScopeAsync(CancellationToken cancellationToken)
    {
        bool initCompleted = false;
        try
        {
            await _scopeProvider.InvokeInScopeAsync(async (provider, ct) =>
            {
                _currentContainer = SafeGetContainer(provider);
                if (_currentContainer is null)
                {
                    return;
                }
                await _currentContainer.InitializeAllAsync(ct);
                initCompleted = true;
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancelled by stoppingToken or signal
        }
        catch
        {
            // Non-OCE init failure — already logged in ScopedLifecycleContainer
            await CleanUpCurrentContainerAsync(default);
        }
        return initCompleted;
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
