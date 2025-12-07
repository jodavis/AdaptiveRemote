using AdaptiveRemote.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

internal class ApplicationLifecycle : BackgroundService
{
    private readonly IApplicationScopeProvider _scopeProvider;
    private readonly ILogger<ApplicationLifecycle> _logger;
    private ScopedLifecycleContainer? _currentContainer;

    public ApplicationLifecycle(IApplicationScopeProvider scopeProvider, ILogger<ApplicationLifecycle> logger)
    {
        _scopeProvider = scopeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = _scopeProvider.InvokeInScopeAsync(InitializeLifecycle, stoppingToken);

        await stoppingToken.WaitForCancelled();

        _logger.LogInformation(Message.ApplicationLifecycle_ShuttingDown);

        await _scopeProvider.InvokeInScopeAsync(CleanUpLifecycle, default);
    }

    private async Task InitializeLifecycle(IServiceProvider provider, CancellationToken cancellationToken)
    {
        ScopedLifecycleContainer container = provider.GetRequiredService<ScopedLifecycleContainer>();

        await container.InitializeAllAsync(cancellationToken);

        _currentContainer = container;
    }

    private async Task CleanUpLifecycle(IServiceProvider provider, CancellationToken token)
    {
        ScopedLifecycleContainer? scope = Interlocked.Exchange(ref _currentContainer, null);

        if (scope != null)
        {
            await scope.CleanUpAllAsync(token);
        }
    }
}
