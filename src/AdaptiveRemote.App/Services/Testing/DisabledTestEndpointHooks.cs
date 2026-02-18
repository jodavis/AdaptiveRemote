
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Testing;

internal class DisabledTestEndpointHooks : ITestEndpointHooks
{
    Task ITestEndpointHooks.InjectHostServiceAsync(IHostBuilder hostBuilder, CancellationToken cancellationToken) => Task.CompletedTask;
    Task ITestEndpointHooks.ProvideServicesToTestAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;
}
