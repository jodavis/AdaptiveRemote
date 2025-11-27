using AdaptiveRemote.Configuration;
using AdaptiveRemote.Models;
using AdaptiveRemote.Services;
using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Lifecycle;
using ElectronNET.API;
using ElectronNET.API.Entities;
using Microsoft.Extensions.DependencyInjection;

// Create accelerated services using the Core pattern
var acceleratedServices = new AcceleratedServices(args);

// Configure Electron-specific services
acceleratedServices.ConfigureHostServices(services =>
{
    // Add Blazor Server services for Electron
    services.AddRazorPages();
    services.AddServerSideBlazor();

    // Add Electron-specific scope factory
    services.AddSingleton<IApplicationScopeFactory, ElectronScopeFactory>();

    // Add fake speech services for Electron (cross-platform without System.Speech)
    services.AddSingleton<ISpeechRecognitionEngine, FakeSpeechRecognitionEngine>();
    services.AddSingleton<ISpeechSynthesizer, FakeSpeechSynthesizer>();
    services.AddScoped<IGrammarProvider, FakeGrammarProvider>();
});

// For Electron, we need to use a different startup pattern
// because it requires the ASP.NET Core web application builder
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseElectron(args);

// Add Blazor services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Configure using Core's configuration
builder.Host.ConfigureCoreApp();

// Add lifecycle services
var lifecycleView = acceleratedServices.ViewModel;
var lifecycleController = acceleratedServices.Controller;
builder.Services.AddSingleton(lifecycleView);
builder.Services.AddSingleton<ILifecycleViewController>(lifecycleController);

// Add Electron-specific services
builder.Services.AddSingleton<IApplicationScopeFactory, ElectronScopeFactory>();
builder.Services.AddSingleton<ISpeechRecognitionEngine, FakeSpeechRecognitionEngine>();
builder.Services.AddSingleton<ISpeechSynthesizer, FakeSpeechSynthesizer>();
builder.Services.AddScoped<IGrammarProvider, FakeGrammarProvider>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Start Electron window - this makes it a desktop app, not just a web server
if (HybridSupport.IsElectronActive)
{
    _ = Task.Run(async () =>
    {
        var window = await Electron.WindowManager.CreateWindowAsync(new BrowserWindowOptions
        {
            Width = 1200,
            Height = 800,
            Show = true,
            Title = "Adaptive Remote"
        });

        window.OnClosed += () => Electron.App.Quit();
    });
}

app.Run();
