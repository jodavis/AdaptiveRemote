using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace AdaptiveRemote;

/// <summary>
/// IApplicationScopeFactory implementation for Blazor Server hosting on Linux
/// </summary>
public class BlazorServerScopeFactory : IApplicationScopeFactory, IApplicationScope
{
    private readonly IServiceProvider _serviceProvider;
    private IServiceScope? _scope;

    public BlazorServerScopeFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<IApplicationScope> CreateNewScopeAsync(CancellationToken cancellationToken)
    {
        _scope?.Dispose();
        _scope = _serviceProvider.CreateScope();
        return Task.FromResult<IApplicationScope>(this);
    }

    public async Task TryInvokeAsync(Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken)
    {
        var scopeServices = _scope?.ServiceProvider ?? _serviceProvider;
        await workItem(scopeServices, cancellationToken);
    }

    public void Dispose()
    {
        _scope?.Dispose();
        _scope = null;
        GC.SuppressFinalize(this);
    }
}
