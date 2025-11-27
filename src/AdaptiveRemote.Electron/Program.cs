using AdaptiveRemote.Configuration;
using AdaptiveRemote.Services.Conversation;
using ElectronNET.API;
using ElectronNET.API.Entities;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseElectron(args);

// Add Blazor services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Configure the host using Core's configuration
builder.Host.ConfigureElectronApp();

// Add Electron-specific lifecycle and services
builder.Services.AddElectronLifecycle();
builder.Services.AddElectronScopeFactory();

// Add fake speech services for Electron (cross-platform without System.Speech)
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

// Start Electron window if running in Electron
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
