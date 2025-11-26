using AdaptiveRemote;
using AdaptiveRemote.Configuration;
using AdaptiveRemote.Models;
using AdaptiveRemote.Services;
using AdaptiveRemote.Services.Lifecycle;
using ElectronNET.API;
using ElectronNET.API.Entities;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseElectron(args);

// Add Blazor services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add core application services
builder.Services.AddRemoteServices();
builder.Services.AddSystemWrappers();

// Add fake conversation services for Linux (no System.Speech)
builder.Services.AddFakeSpeechServices();

// Add lifecycle view model and controller
var lifecycleView = new LifecycleView();
var lifecycleController = new LifecycleViewController(lifecycleView);
builder.Services.AddSingleton(lifecycleView);
builder.Services.AddSingleton<ILifecycleViewController>(lifecycleController);

// Register IApplicationScopeFactory for the lifecycle
builder.Services.AddSingleton<IApplicationScopeFactory, BlazorServerScopeFactory>();

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
