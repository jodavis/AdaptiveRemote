using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

internal static class TestingHostBuilderExtensions
{
    /// <summary>
    /// Adds test control services for E2E testing.
    /// The test control endpoint is only activated when --test:ControlPort is provided.
    /// </summary>
    internal static IHostBuilder AddTestControlEndpoint(this IHostBuilder builder)
        => builder.ConfigureServices(services =>
        {
            services.AddHostedService<TestControlService>();
        });
}
