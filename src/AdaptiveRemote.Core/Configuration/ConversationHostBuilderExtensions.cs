using AdaptiveRemote.Services;
using AdaptiveRemote.Services.Conversation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            .AddScoped<ConversationStateMachine>()
            .AddSingleton<IListeningController, ListeningController>()
            .AddScoped(GetConversationViewModel);

    internal static IServiceCollection AddConversationServices(this IServiceCollection services, IConfiguration config)
        => services
            .AddConversationServices()
            .OptionallyAddFakeSpeechRecognition(config)
            .Configure<ConversationSettings>(config);

    private static Models.ConversationView GetConversationViewModel(IServiceProvider provider)
    {
        IRemoteDefinitionService definition = provider.GetRequiredService<IRemoteDefinitionService>();
        return definition.GetElement<Models.ConversationView>();
    }

    private static IServiceCollection OptionallyAddFakeSpeechRecognition(this IServiceCollection services, IConfiguration config)
        => config.GetValue<bool>(nameof(ConversationSettings.Fake)) == true
            ? services
                .AddSingleton<ISpeechRecognitionEngine, FakeSpeechRecognitionEngine>()
                .AddScoped<IGrammarProvider, FakeGrammarProvider>()
            : services;
}
