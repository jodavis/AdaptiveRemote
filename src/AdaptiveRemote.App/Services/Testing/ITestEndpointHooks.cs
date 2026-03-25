using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Testing;

internal interface ITestEndpointHooks
{
    /// <summary>
    /// Called during host startup to allow tests to inject services before the host is built.
    /// This method should block until the test has finished adding services via AddTestServiceAsync.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task InjectHostServiceAsync(IHostBuilder hostBuilder, CancellationToken cancellationToken);

    /// <summary>
    /// Called after the host has been built to provide the service provider to tests.
    /// This allows tests to create test services that depend on application services.
    /// </summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task ProvideServicesToTestAsync(IServiceProvider services, CancellationToken cancellationToken);
}
