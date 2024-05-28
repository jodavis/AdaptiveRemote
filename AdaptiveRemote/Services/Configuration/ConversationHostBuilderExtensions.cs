using AdaptiveRemote.Services.Conversation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Configuration;

internal static class ConversationHostBuilderExtensions
{
    internal static IHostBuilder AddConversationSystem(this IHostBuilder hostBuilder)
        => hostBuilder.ConfigureServices((context, services) => services.AddConversationServices(context.Configuration.GetSection("Conversation")));

    internal static IServiceCollection AddConversationServices(IServiceCollection services)
        => services
            .AddScoped<IConversationController, ConversationController>()
            .AddScoped<ISpeechRecognition, SpeechRecognition>()
            .AddScoped<ISpeechSynthesis, SpeechSynthesis>()
            .AddScoped<IGrammarProvider, StaticGrammarProvider>()
            .AddSingleton<ISpeechSynthesizer, SpeechSynthesizerWrapper>()
            .AddSingleton<ISpeechRecognitionEngine, SpeechRecognitionEngineWrapper>()
            .AddSingleton<IAudioConfigurationService, DefaultDeviceAudioConfiguration>()
            .AddScoped(GetConversationViewModel);

    internal static IServiceCollection AddConversationServices(this IServiceCollection services, IConfiguration config)
        => AddConversationServices(services).Configure<ConversationSettings>(config);

    private static Models.ConversationView GetConversationViewModel(IServiceProvider provider)
    {
        IRemoteDefinitionService definition = provider.GetRequiredService<IRemoteDefinitionService>();
        return definition.GetElement<Models.ConversationView>();
    }
}
