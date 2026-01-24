using AdaptiveRemote.Headless;
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

// Use existing AcceleratedServices pattern to configure host builder
AcceleratedServices accelerated = new(args);
accelerated.ConfigureHost(builder.Host);

// Stub speech services to satisfy DI for headless operation
builder.Services
    .AddSingleton<ISpeechSynthesizer, StubSpeechSynthesizer>()
    .AddSingleton<IGrammarProvider, StubGrammarProvider>()
    .AddSingleton<ISpeechRecognitionEngine, StubSpeechRecognition>();

// Add the minimal services for ASP.NET to serve the Blazor UI
// Use the new .NET 8+ Razor Components API instead of the deprecated ServerSideBlazor
builder.Services.AddRazorPages();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register circuit handler for logging
builder.Services.AddSingleton<CircuitHandler, LoggingCircuitHandler>();

// Register Playwright hosted service to manage browser lifecycle
// Register as singleton first so it can be injected into test services
builder.Services.AddSingleton<PlaywrightBrowserLifetimeService>();
builder.Services.AddSingleton<IBrowserUIAccess>(sp => sp.GetRequiredService<PlaywrightBrowserLifetimeService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<PlaywrightBrowserLifetimeService>());
builder.Services.Configure<PlaywrightSettings>(builder.Configuration.GetSection("playwright"));

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

app.UseAntiforgery();

app.UseRouting();

// Map Razor pages for _Host.cshtml
app.MapRazorPages();

// Use the new Razor Components API which serves the framework JavaScript automatically
app.MapRazorComponents<AdaptiveRemote.Components.Root>()
    .AddInteractiveServerRenderMode();

app.Run();
