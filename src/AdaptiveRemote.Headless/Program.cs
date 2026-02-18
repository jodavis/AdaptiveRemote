using AdaptiveRemote;
using AdaptiveRemote.Headless;
using AdaptiveRemote.Headless.Components;
using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Testing;
using Microsoft.AspNetCore.Components.Server.Circuits;

await new HeadlessAppHostRunner(args)
    .RunAsync(CancellationToken.None);

internal class HeadlessAppHostRunner : AppHostRunner
{
    private readonly WebApplicationBuilder _applicationBuilder;

    public HeadlessAppHostRunner(string[] args)
        : base(args)
    {
        WebApplicationOptions options = new()
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = args
        };

        _applicationBuilder = WebApplication.CreateBuilder(options);
    }

    protected override IHostBuilder ConfigureHostBuilder()
    {
        return _applicationBuilder
            .ConfigureStubSpeechServices()
            .ConfigureBlazorServices()
            .ConfigurePlaywrightBrowser()
            .Host;
    }

    protected override IHost BuildHost(IHostBuilder hostBuilder) =>
        _applicationBuilder
            .Build()
            .AddHostingRoutes();

    protected override Task RunHostAsync(IHost host, CancellationToken cancellationToken)
    {
        if (host is not WebApplication webApp)
        {
            throw new InvalidOperationException("Expected host to be a WebApplication");
        }

        return webApp.RunAsync(cancellationToken);
    }
}

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
