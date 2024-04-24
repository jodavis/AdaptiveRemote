using AdaptiveRemote.Services.Speech;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Configuration;

internal static class SpeechHostBuilderExtensions
{
    internal static IHostBuilder AddSpeechServices(this IHostBuilder hostBuilder)
        => hostBuilder.ConfigureServices(services => AddSpeechServices(services));

    internal static IServiceCollection AddSpeechServices(IServiceCollection services)
        => services
            .AddScoped<ISpeechController, SpeechController>()
            .AddScoped<ISpeechRecognition, SpeechRecognition>()
            .AddScoped<ISpeechSynthesis, SpeechSynthesis>()
            .AddSingleton<ISpeechSynthesizer, SpeechSynthesizerWrapper>()
            .AddSingleton<ISpeechRecognitionEngine, SpeechRecognitionEngineWrapper>()
            .AddScoped(GetListeningViewModel);

    private static Models.Listening GetListeningViewModel(IServiceProvider provider)
    {
        IRemoteDefinitionService definition = provider.GetRequiredService<IRemoteDefinitionService>();
        return definition.GetElement<Models.Listening>();
    }
}
