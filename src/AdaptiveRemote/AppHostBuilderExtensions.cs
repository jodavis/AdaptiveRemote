using AdaptiveRemote.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote;

public static class AppHostBuilderExtensions
{
    public static IHostBuilder ConfigureApp(this IHostBuilder hostBuilder)
        => hostBuilder
            .AddBlazorUI()
            .ConfigureTelemetry()
            .AddRemoteServices()
            .AddBroadlinkSupport()
            .AddTiVoSupport()
            .AddConversationSystem()
            .AddSystemWrapperServices();

    public static IHostBuilder ConfigureAppSettings(this IHostBuilder hostBuilder, string[] args)
        => hostBuilder
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // This makes the default behavior to publish telemetry when the full application
                    // is hosted. However, the setting is "false" by default so that test hosting won't
                    // publish telemetry unless explicitly enabled.
                    // TODO [ADR-12]: This behavior is currently disabled because we don't have anywhere
                    // to publish telemetry to.
                    // ["telemetry:Publish"] = "True"
                });
                config.AddUserSecrets<App>();
                config.AddCommandLine(args);
            });

    private static IHostBuilder AddBlazorUI(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureServices(services => services
                .AddWpfBlazorWebView());
    }
}
