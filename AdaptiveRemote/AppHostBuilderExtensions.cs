using AdaptiveRemote.Services.Configuration;
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
            .AddConversationSystem();

    public static IHostBuilder ConfigureAppSettings(this IHostBuilder hostBuilder, string[] args)
        => hostBuilder
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["telemetry:Publish"] = "True"
                });
                config.AddUserSecrets<App>();
                config.AddCommandLine(args);
            });

    private static IHostBuilder AddBlazorUI(this IHostBuilder hostBuilder)
        => hostBuilder.ConfigureServices(services => services
            .AddSingleton<MainWindow>()
            .AddWpfBlazorWebView());
}
