using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
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
            if (await RunPreInitializersAsync(stoppingToken))
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _signal.Reset();

                    using CancellationTokenSource linkedCts =
                        CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _signal.Token);

                    _logger.ApplicationLifecycle_WaitingForScope();

                    bool initialized = await InitializeScopeAsync(linkedCts.Token);
                    if (!linkedCts.Token.IsCancellationRequested)
                    {
                        if (initialized)
                        {
                            // Scope is ready; block until stoppingToken or signal.Token fires.
                            _logger.ApplicationLifecycle_ScopeReady();
                            await linkedCts.Token.WaitForCancelledAsync();
                        }
                        else
                        {
                            // A fatal error occurred and was logged.
                            break;
                        }
                    }

                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await CleanUpCurrentContainerAsync(default);

                    _logger.ApplicationLifecycle_RecyclingScope();
                    await _scopeProvider.RecycleScopeAsync();
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Do nothing, shutdown was requested
        }
        catch (Exception ex)
        {
            _logger.ApplicationLifecycle_UnhandledError(ex);
        }

        _logger.ApplicationLifecycle_ShuttingDown();
        await CleanUpCurrentContainerAsync(default);
    }

    private async Task<bool> RunPreInitializersAsync(CancellationToken stoppingToken)
    {
        try
        {
            Task[] initTasks = _preInitializers.Select(init => RunSinglePreInitializerAsync(init, stoppingToken)).ToArray();
            await Task.WhenAll(initTasks);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task RunSinglePreInitializerAsync(IPreScopeInitializer initializer, CancellationToken stoppingToken)
    {
        using ILifecycleActivity activity = _viewController.StartTask(Phrases.Startup_Preinitializing(initializer.Name));
        try
        {
            Task waitTask = initializer.WaitAsync(activity, stoppingToken);
            if (!waitTask.IsCompleted)
            {
                _logger.ApplicationLifecycle_WaitingForPreinitializer(initializer.Name);
            }
            await waitTask;
        }
        catch (Exception error)
        {
            _logger.ApplicationLifecycle_PreinitializerFailed(initializer.Name, error);
            activity.SetFatalError(error);
            throw;
        }
    }

    private async Task<bool> InitializeScopeAsync(CancellationToken cancellationToken)
    {
        bool initialized = false;
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
                initialized = true;
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancelled by stoppingToken or signal
        }
        catch
        {
            // Exceptions from scope creation or initialization are already handled and logged; no need to log again.
        }

        return initialized;
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
