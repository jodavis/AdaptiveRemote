using AdaptiveRemote.Services.Conversation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Configuration;

internal static class ConversationHostBuilderExtensions
{
    internal static IHostBuilder AddConversationServices(this IHostBuilder hostBuilder)
        => hostBuilder.ConfigureServices(services => AddConversationServices(services));

    internal static IServiceCollection AddConversationServices(IServiceCollection services)
        => services
            .AddScoped<IConversationController, ConversationController>()
            .AddScoped<ISpeechRecognition, SpeechRecognition>()
            .AddScoped<ISpeechSynthesis, SpeechSynthesis>()
            .AddScoped<IGrammarProvider, StaticGrammarProvider>()
            .AddSingleton<ISpeechSynthesizer, SpeechSynthesizerWrapper>()
            .AddSingleton<ISpeechRecognitionEngine, SpeechRecognitionEngineWrapper>()
            .AddScoped(GetConversationViewModel);

    private static Models.ConversationView GetConversationViewModel(IServiceProvider provider)
    {
        IRemoteDefinitionService definition = provider.GetRequiredService<IRemoteDefinitionService>();
        return definition.GetElement<Models.ConversationView>();
    }
}
