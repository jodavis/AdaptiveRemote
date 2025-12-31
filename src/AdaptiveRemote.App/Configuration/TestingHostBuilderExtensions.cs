using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

internal static class TestingHostBuilderExtensions
{
    /// <summary>
    /// Optionally adds test control services for E2E testing.
    /// The test control endpoint is only added when --test:ControlPort is provided.
    /// </summary>
    internal static IHostBuilder OptionallyAddTestHookEndpoint(this IHostBuilder builder)
        => builder.ConfigureServices((context, services) =>
        {
            // Only add the service if test:ControlPort is configured
            int? controlPort = context.Configuration.GetValue<int?>($"{SettingsKeys.Testing}:{nameof(TestingSettings.ControlPort)}");
            
            if (controlPort.HasValue)
            {
                services.Configure<TestingSettings>(context.Configuration.GetSection(SettingsKeys.Testing));
                services.AddHostedService<TestEndpointService>();
            }
        });
}
