using AdaptiveRemote.Configuration;
using AdaptiveRemote.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote;

public static class AppHostBuilderExtensions
{
    public static IHostBuilder ConfigureApp(this IHostBuilder hostBuilder)
        => hostBuilder
            .ConfigureTelemetry()
            .AddRemoteServices()
            .AddBroadlinkSupport()
            .AddTiVoSupport()
            .AddConversationSystem()
            .AddSystemWrapperServices()
            // Ensure logging configured early so FileLoggerProvider captures all logs including RPC-handled test logs
            .ConfigureLogging((context, logging) =>
            {
                string? hostLogFile = context.Configuration.GetValue<string>("log:FilePath");
                if (!string.IsNullOrEmpty(hostLogFile))
                {
                    logging.AddProvider(new FileLoggerProvider(hostLogFile));
                }
            })
            .OptionallyAddTestControlEndpoint();

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
                config.AddUserSecrets<UserSecretsKey>();
                config.AddCommandLine(args);
            });

    // This class is used to locate the user secrets assembly for this project.
    private class UserSecretsKey { }
}
