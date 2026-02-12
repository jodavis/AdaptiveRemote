using AdaptiveRemote.Headless;
using AdaptiveRemote.Headless.Components;
using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Lifecycle;
using AdaptiveRemote.Services.Testing;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using StreamJsonRpc;

WebApplicationOptions options = new()
{
    ContentRootPath = AppContext.BaseDirectory,
    Args = args
};

WebApplicationBuilder builder = WebApplication.CreateBuilder(options);

// Configure accelerated services
AcceleratedServices accelerated = new(args);

// If in test mode, set up early test endpoint listener
EarlyTestEndpointListener? earlyListener = null;
TestEndpointCoordinator? testCoordinator = null;
ITestEndpoint? testEndpoint = null;

if (builder.Configuration.GetValue<int?>("test:ControlPort").HasValue)
{
    // Create test coordinator
    ILoggerFactory loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
    ILogger<TestEndpointCoordinator> coordLogger = loggerFactory.CreateLogger<TestEndpointCoordinator>();
    testCoordinator = new TestEndpointCoordinator(builder.Configuration, coordLogger);

    // Initialize accelerated services with coordinator
    accelerated.InitializeTestCoordinator(builder.Configuration, loggerFactory);

    // Create TestEndpointService early via factory
    testEndpoint = accelerated.CreateEarlyTestEndpoint(builder.Configuration, loggerFactory);
    
    if (testEndpoint != null)
    {
        // Start early listener with forwarding to TestEndpointService
        ILogger<EarlyTestEndpointListener> listenerLogger = loggerFactory.CreateLogger<EarlyTestEndpointListener>();
        earlyListener = new EarlyTestEndpointListener(builder.Configuration, testCoordinator, listenerLogger);
        earlyListener.StartListening();

        // Wait for test connection
        if (!earlyListener.WaitForConnection(TimeSpan.FromSeconds(30), testEndpoint))
        {
            Console.Error.WriteLine("Failed to establish test connection within timeout");
            Environment.Exit(1);
            return;
        }

        // Wait for test to register services and signal ready
        if (!accelerated.WaitForTestInitialization())
        {
            Console.Error.WriteLine("Test initialization timeout");
            Environment.Exit(1);
            return;
        }

        // Stop listening for new connections (we have the one we need)
        earlyListener.StopListening();
    }
}

// Configure app services
accelerated.ConfigureHost(builder.Host);

// Add test coordinator to services if available
if (testCoordinator != null)
{
    builder.Services.AddSingleton(testCoordinator);
}

// Add pre-created TestEndpoint as HostedService if available
if (testEndpoint is IHostedService hostedService)
{
    builder.Services.AddSingleton(hostedService);
}

// Configure other services
builder
    .ConfigureStubSpeechServices()
    .ConfigureBlazorServices()
    .ConfigurePlaywrightBrowser();

// Build the app
WebApplication app = builder.Build();

// Dispose early listener (connection is now handled by TestEndpointService)
earlyListener?.Dispose();

// Add routes and run
app.AddHostingRoutes().Run();

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
