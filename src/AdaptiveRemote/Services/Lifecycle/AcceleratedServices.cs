using AdaptiveRemote.Configuration;
using AdaptiveRemote.Models;
using AdaptiveRemote.Services.Conversation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// Windows-specific accelerated services that extends the Core AcceleratedServices
/// with Windows-specific services like MainWindow and Windows speech recognition.
/// </summary>
public class WindowsAcceleratedServices
{
    private readonly AcceleratedServices _acceleratedServices;

    /// <summary>
    /// The WPF main window
    /// </summary>
    public MainWindow MainWindow { get; }

    /// <summary>
    /// The lifecycle view model used to display startup status
    /// </summary>
    public LifecycleView ViewModel => _acceleratedServices.ViewModel;

    /// <summary>
    /// The lifecycle view controller used to update startup status
    /// </summary>
    public ILifecycleViewController Controller => _acceleratedServices.Controller;

    public WindowsAcceleratedServices(string[] args)
    {
        _acceleratedServices = new AcceleratedServices(args);

        MainWindow = new(ViewModel);

        _acceleratedServices
            .ConfigureAppSettings(config =>
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
            })
            .ConfigureHostServices(services =>
            {
                // Add Windows-specific pre-created services
                services.AddSingleton(MainWindow);
                services.AddWpfBlazorWebView();
                services.AddSingleton<IApplicationScopeFactory, BlazorWindowScopeFactory>();
            })
            .ConfigureHostServices((context, services) =>
            {
                // Add Windows speech services based on configuration
                var config = context.Configuration.GetSection(SettingsKeys.Conversation);
                bool useFake = config.GetValue<bool>(nameof(ConversationSettings.Fake));

                if (useFake)
                {
                    // Use fake speech recognition for testing without speaking aloud
                    services
                        .AddScoped<IGrammarProvider, StaticGrammarProvider>()
                        .AddSingleton<ISpeechSynthesizer, SpeechSynthesizerWrapper>()
                        .AddSingleton<ISpeechRecognitionEngine, FakeSpeechRecognitionEngine>()
                        .AddSingleton<IAudioConfigurationService, DefaultDeviceAudioConfiguration>();
                }
                else
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

    public Task RunApplicationLoopAsync() => _acceleratedServices.RunApplicationLoopAsync();
}
