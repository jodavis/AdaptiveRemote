using AdaptiveRemote.Configuration;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote;

public static class AppHostBuilderExtensions
{
    private const string KeyVaultName = "adaptiveremote";

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
                    ["telemetry:Publish"] = "True"
                });
                config.AddAzureKeyVault(new Uri($"https://{KeyVaultName}.vault.azure.net/"), new DefaultAzureCredential());
                config.AddUserSecrets<App>();
                config.AddCommandLine(args);
            });

    private static IHostBuilder AddBlazorUI(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureServices(services => services
                .AddWpfBlazorWebView());
    }
}
