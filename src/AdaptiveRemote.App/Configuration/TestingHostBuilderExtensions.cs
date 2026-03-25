using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

internal static class TestingHostBuilderExtensions
{
    private const string ControlPortSettingKey = $"{SettingsKeys.Testing}:{nameof(TestingSettings.ControlPort)}";

    /// <summary>
    /// Optionally adds test control services for E2E testing.
    /// The test control endpoint is only added when --test:ControlPort is provided.
    /// </summary>
    internal static IHostBuilder OptionallyAddTestHookEndpoint(this IHostBuilder builder)
        => builder.ConfigureServices((context, services) =>
        {
            // Only add the service if test:ControlPort is configured
            int? controlPort = context.Configuration.GetValue<int?>(ControlPortSettingKey);

            if (controlPort.HasValue)
            {
                services.Configure<TestingSettings>(context.Configuration.GetSection(SettingsKeys.Testing));
                // Register TestServiceProvider for creating test services after host is built
                services.AddSingleton<ITestServiceProvider, TestServiceProvider>();
            }
        });
}
