using AdaptiveRemote.Services;
using AdaptiveRemote.Services.Conversation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

public static class ConversationHostBuilderExtensions
{
    public static IHostBuilder AddConversationSystem(this IHostBuilder hostBuilder)
        => hostBuilder.ConfigureServices((context, services) => services.AddConversationServices(context.Configuration.GetSection(SettingsKeys.Conversation)));

    public static IServiceCollection AddConversationServices(this IServiceCollection services)
        => services
            .AddScopedLifecycleService<ConversationController>()
            .AddScoped<ISpeechRecognition, SpeechRecognition>()
            .AddScoped<ISpeechSynthesis, SpeechSynthesis>()
            .AddScoped<ConversationStateMachine>()
            .AddSingleton<IListeningController, ListeningController>()
            .AddScoped(GetConversationViewModel);

    public static IServiceCollection AddConversationServices(this IServiceCollection services, IConfiguration config)
        => services
            .AddConversationServices()
            .OptionallyAddFakeSpeechRecognition(config)
            .Configure<ConversationSettings>(config);

    /// <summary>
    /// Adds fake speech services for cross-platform scenarios without System.Speech
    /// </summary>
    public static IServiceCollection AddFakeSpeechServices(this IServiceCollection services)
        => services
            .AddSingleton<ISpeechRecognitionEngine, FakeSpeechRecognitionEngine>()
            .AddSingleton<ISpeechSynthesizer, FakeSpeechSynthesizer>()
            .AddScoped<IGrammarProvider, FakeGrammarProvider>();

    private static Models.ConversationView GetConversationViewModel(IServiceProvider provider)
    {
        IRemoteDefinitionService definition = provider.GetRequiredService<IRemoteDefinitionService>();
        return definition.GetElement<Models.ConversationView>();
    }

    private static IServiceCollection OptionallyAddFakeSpeechRecognition(this IServiceCollection services, IConfiguration config)
        => config.GetValue<bool>(nameof(ConversationSettings.Fake)) == true
            ? services.AddFakeSpeechServices()
            : services;
}
