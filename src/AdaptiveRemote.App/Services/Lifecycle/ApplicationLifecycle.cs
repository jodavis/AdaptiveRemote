using AdaptiveRemote.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

internal class ApplicationLifecycle : BackgroundService
{
    private readonly IApplicationScopeProvider _scopeProvider;
    private readonly ILogger<ApplicationLifecycle> _logger;
    private ScopedLifecycleContainer? _currentScope;

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
        _currentScope = provider.GetRequiredService<ScopedLifecycleContainer>();

        await _currentScope.InitializeAllAsync(cancellationToken);
    }

    private async Task CleanUpLifecycle(IServiceProvider provider, CancellationToken token)
    {
        ScopedLifecycleContainer? scope = Interlocked.Exchange(ref _currentScope, null);

        if (scope != null)
        {
            await scope.CleanUpAllAsync(token);
        }
    }
}
