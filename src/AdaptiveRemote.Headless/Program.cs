using AdaptiveRemote.Headless;
using AdaptiveRemote.Headless.Components;
using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Lifecycle;
using AdaptiveRemote.Services.Testing;
using Microsoft.AspNetCore.Components.Server.Circuits;

WebApplicationOptions options = new()
{
    ContentRootPath = AppContext.BaseDirectory,
    Args = args
};

WebApplicationBuilder builder = WebApplication.CreateBuilder(options);

// Configure accelerated services
AcceleratedServices accelerated = new(args);
accelerated.ConfigureHost(builder.Host);

// Configure other services
builder
    .ConfigureStubSpeechServices()
    .ConfigureBlazorServices()
    .ConfigurePlaywrightBrowser();

// Build and run
builder
    .Build()
    .AddHostingRoutes()
    .Run();

internal static class Configuration
{
    internal static WebApplicationBuilder ConfigureStubSpeechServices(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddSingleton<ISpeechSynthesizer, StubSpeechSynthesizer>()
            .AddSingleton<IGrammarProvider, StubGrammarProvider>()
            .AddSingleton<ISpeechRecognitionEngine, StubSpeechRecognition>();

        return builder;
    }

    internal static WebApplicationBuilder ConfigureBlazorServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Register circuit handler for logging
        builder.Services.AddSingleton<CircuitHandler, LoggingCircuitHandler>();

        return builder;
    }

    internal static WebApplicationBuilder ConfigurePlaywrightBrowser(this WebApplicationBuilder builder)
    {
        // Register Playwright hosted service to manage browser lifecycle
        // Register as singleton first so it can be injected into test services
        builder.Services.AddSingleton<PlaywrightBrowserLifetimeService>();
        builder.Services.AddSingleton<IBrowserUIAccess>(sp => sp.GetRequiredService<PlaywrightBrowserLifetimeService>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<PlaywrightBrowserLifetimeService>());
        builder.Services.Configure<PlaywrightSettings>(builder.Configuration.GetSection("playwright"));

        return builder;
    }

    internal static WebApplication AddHostingRoutes(this WebApplication app)
    {
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
        return app;
    }
}
