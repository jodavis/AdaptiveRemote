using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote;

internal static class AppHostConfiguration
{
    internal static IServiceCollection AddWindowsUIServices(this IServiceCollection services, MainWindow mainWindow)
    {
        // UI-related host services
        services.AddSingleton(mainWindow);
        services.AddSingleton<IApplicationScopeFactory, BlazorWindowScopeFactory>();
        services.AddWpfBlazorWebView();

        return services;
    }

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
