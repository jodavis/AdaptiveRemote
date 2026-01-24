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

// Use existing AcceleratedServices pattern to configure host builder
AcceleratedServices accelerated = new(args);
accelerated.ConfigureHost(builder.Host);

// Stub speech services to satisfy DI for headless operation
builder.Services
    .AddSingleton<ISpeechSynthesizer, StubSpeechSynthesizer>()
    .AddSingleton<IGrammarProvider, StubGrammarProvider>()
    .AddSingleton<ISpeechRecognitionEngine, StubSpeechRecognition>();

// Add services to the container - using the .NET 10 template approach
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

// Configure the HTTP request pipeline - following .NET 10 template pattern
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
