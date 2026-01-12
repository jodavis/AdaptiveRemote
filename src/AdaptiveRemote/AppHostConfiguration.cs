using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote;

internal static class AppHostConfiguration
{
    internal static IHostBuilder AddAcceleratedServices(this IHostBuilder builder, AcceleratedServices acceleratedServices)
    {
        acceleratedServices.ConfigureHost(builder);
        return builder;
    }

    internal static IHostBuilder AddWindowsUIServices(this IHostBuilder builder)
        => builder.ConfigureServices(services => services.AddWindowsUIServices());

    internal static IServiceCollection AddWindowsUIServices(this IServiceCollection services)
    {
        // UI-related host services
        services.AddHostedService<BlazorWindowServicesSetter>();
        services.AddWpfBlazorWebView();

        return services;
    }

    internal static IHostBuilder AddWindowsSpeechServices(this IHostBuilder builder)
        => builder.ConfigureServices(services => services.AddWindowsSpeechServices());

    internal static IServiceCollection AddWindowsSpeechServices(this IServiceCollection services)
    {
        // System.Speech host services
        services.AddScoped<IGrammarProvider, StaticGrammarProvider>();
        services.AddSingleton<ISpeechSynthesizer, SpeechSynthesizerWrapper>();
        services.AddSingleton<ISpeechRecognitionEngine, SpeechRecognitionEngineWrapper>();
        services.AddSingleton<IAudioConfigurationService, DefaultDeviceAudioConfiguration>();

        return services;
    }
}
