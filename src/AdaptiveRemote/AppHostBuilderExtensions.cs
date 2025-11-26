using AdaptiveRemote.Configuration;
using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote;

public static class AppHostBuilderExtensions
{
    public static IHostBuilder ConfigureApp(this IHostBuilder hostBuilder)
        => hostBuilder
            .AddBlazorUI()
            .ConfigureTelemetry()
            .AddRemoteServices()
            .AddWindowsServices()
            .AddBroadlinkSupport()
            .AddTiVoSupport()
            .AddConversationSystem()
            .AddWindowsConversationServices()
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

    private static IHostBuilder AddWindowsServices(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureServices(services => services
            .AddSingleton<IApplicationScopeFactory, BlazorWindowScopeFactory>());
    }

    private static IHostBuilder AddWindowsConversationServices(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureServices((context, services) =>
        {
            var config = context.Configuration.GetSection(SettingsKeys.Conversation);
            bool useFake = config.GetValue<bool>(nameof(ConversationSettings.Fake));

            if (!useFake)
            {
                services
                    .AddScoped<IGrammarProvider, StaticGrammarProvider>()
                    .AddSingleton<ISpeechSynthesizer, SpeechSynthesizerWrapper>()
                    .AddSingleton<ISpeechRecognitionEngine, SpeechRecognitionEngineWrapper>()
                    .AddSingleton<IAudioConfigurationService, DefaultDeviceAudioConfiguration>();
            }

            // Add SamplesRecorder if configured
            if (config.GetValue<bool>(nameof(ConversationSettings.RecordSamples)))
            {
                services
                    .AddHostedService<SamplesRecorder>()
                    .AddSingleton<ILoggerProvider, SamplesRecorder.LoggerProvider>();
            }
        });
    }
}
