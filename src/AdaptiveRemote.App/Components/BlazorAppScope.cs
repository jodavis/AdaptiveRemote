using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Components;

/// <summary>
/// This object is injected into the root Razor component to capture the IServiceProvider
/// that will be used as the application's DI scope.
/// </summary>
internal class BlazorAppScope : IApplicationScope, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BlazorAppScope> _logger;
    private readonly IApplicationScopeContainer _scopeContainer;

    public BlazorAppScope(IApplicationScopeContainer scopeContainer, IServiceProvider serviceProvider, ILogger<BlazorAppScope> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _scopeContainer = scopeContainer;

        _ = PushSelfToScopeContainerAsync();
    }

    private async Task PushSelfToScopeContainerAsync()
    {
        try
        {
            _logger.LogInformation("Setting new Blazor application scope.");
            await _scopeContainer.SetScopeAsync(this).ConfigureAwait(false);
            _logger.LogInformation("Application scope is initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set Blazor application scope.");
        }
    }

    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("Blazor application scope is being disposed.");
        return new(_scopeContainer.ReleaseScopeAsync(this));
    }

    public Task InvokeInScopeAsync(Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken)
    {
        return workItem(_serviceProvider, cancellationToken);
    }

    public Task RecycleAsync()
    {
        _logger.LogInformation("Recycling Blazor application scope.");

        // In a real implementation, this would refresh the browser which should result
        // in a new scope
        throw new NotImplementedException();
    }
}
