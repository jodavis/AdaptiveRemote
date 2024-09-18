using AdaptiveRemote.Services;
using AdaptiveRemote.Services.Conversation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Configuration;

internal static class ConversationHostBuilderExtensions
{
    internal static IHostBuilder AddConversationSystem(this IHostBuilder hostBuilder)
        => hostBuilder.ConfigureServices((context, services) => services.AddConversationServices(context.Configuration.GetSection(SettingsKeys.Conversation)));

    internal static IServiceCollection AddConversationServices(this IServiceCollection services)
        => services
            .AddScopedLifecycleService<ConversationController>()
            .AddScoped<ISpeechRecognition, SpeechRecognition>()
            .AddScoped<ISpeechSynthesis, SpeechSynthesis>()
            .AddScoped<IGrammarProvider, StaticGrammarProvider>()
            .AddScoped<ConversationStateMachine>()
            .AddSingleton<ISpeechSynthesizer, SpeechSynthesizerWrapper>()
            .AddSingleton<ISpeechRecognitionEngine, SpeechRecognitionEngineWrapper>()
            .AddSingleton<IAudioConfigurationService, DefaultDeviceAudioConfiguration>()
            .AddSingleton<IListeningController, ListeningController>()
            .AddScoped(GetConversationViewModel);

    internal static IServiceCollection AddConversationServices(this IServiceCollection services, IConfiguration config)
        => services
            .AddConversationServices()
            .OptionallyAddFakeSpeechRecognition(config)
            .OptionallyAddSamplesRecorder(config)
            .Configure<ConversationSettings>(config);

    private static Models.ConversationView GetConversationViewModel(IServiceProvider provider)
    {
        IRemoteDefinitionService definition = provider.GetRequiredService<IRemoteDefinitionService>();
        return definition.GetElement<Models.ConversationView>();
    }

    private static IServiceCollection OptionallyAddFakeSpeechRecognition(this IServiceCollection services, IConfiguration config)
        => config.GetValue<bool>(nameof(ConversationSettings.Fake)) == true
            ? services.AddSingleton<ISpeechRecognitionEngine, FakeSpeechRecognitionEngine>()
            : services;

    private static IServiceCollection OptionallyAddSamplesRecorder(this IServiceCollection services, IConfiguration config)
        => config.GetValue<bool>(nameof(ConversationSettings.RecordSamples)) == false
            ? services
            : services
                .AddHostedService<SamplesRecorder>()
                .AddSingleton<ILoggerProvider, SamplesRecorder.LoggerProvider>();
}
