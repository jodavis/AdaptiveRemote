using AdaptiveRemote.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Lifecycle;

internal class ApplicationLifecycle : BackgroundService
{
    private readonly IApplicationScopeFactory _scopeFactory;

    public ApplicationLifecycle(IApplicationScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IApplicationScope scope = await _scopeFactory.CreateNewScopeAsync(default);

        _ = scope.TryInvokeAsync(InitializeLifecycle, default);

        await stoppingToken.WaitForCancelled();
    }

    private Task InitializeLifecycle(IServiceProvider provider, CancellationToken token)
    {
        foreach (IScopedLifecycle scopedService in provider.GetServices<IScopedLifecycle>())
        {
            _ = scopedService.InitializeAsync(default);
        }

        return Task.CompletedTask;
    }
}
