using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Testing;

internal interface ITestEndpointHooks
{
    Task InjectHostServiceAsync(IHostBuilder hostBuilder, CancellationToken cancellationToken);
    Task ProvideServicesToTestAsync(IServiceProvider services, CancellationToken cancellationToken);
}
