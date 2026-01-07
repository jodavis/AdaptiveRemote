using AdaptiveRemote.Headless;
using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Lifecycle;
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
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Register circuit handler for logging
builder.Services.AddSingleton<CircuitHandler, LoggingCircuitHandler>();

// Register Playwright hosted service to manage browser lifecycle
builder.Services.Configure<PlaywrightSettings>(builder.Configuration.GetSection("playwright"));
builder.Services.AddHostedService<PlaywrightBrowserLifetimeService>();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
