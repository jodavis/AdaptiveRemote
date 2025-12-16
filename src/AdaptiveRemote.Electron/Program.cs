using AdaptiveRemote.Electron;
using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Lifecycle;
using ElectronNET.API;
using ElectronNET.API.Entities;
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

// Stub speech services to satisfy DI for prototype
builder.Services
    .AddSingleton<ISpeechSynthesizer, StubSpeechSynthesizer>()
    .AddSingleton<IGrammarProvider, StubGrammarProvider>()
    .AddSingleton<ISpeechRecognitionEngine, StubSpeechRecognition>();

// Also add the minimal services for ASP.NET to serve the Blazor UI
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Register circuit handler for logging
builder.Services.AddSingleton<CircuitHandler, LoggingCircuitHandler>();

// For Linux/CI environments, remove or disable the chrome-sandbox to avoid SUID errors
if (OperatingSystem.IsLinux())
{
    string sandboxPath = Path.Combine(AppContext.BaseDirectory, ".electron/node_modules/electron/dist/chrome-sandbox");
    if (File.Exists(sandboxPath))
    {
        // Rename the sandbox file so Electron will run without it
        try
        {
            File.Move(sandboxPath, sandboxPath + ".disabled", overwrite: true);
        }
        catch
        {
            // If we can't disable it, that's okay - the environment variables should handle it
        }
    }
}

builder.UseElectron(args, onAppReadyCallback: async () =>
{
    BrowserWindowOptions options = new BrowserWindowOptions
    {
        Show = true,
        AutoHideMenuBar = true,
        Width = 2000,
        Height = 1200,
    };

    BrowserWindow browserWindow = await Electron.WindowManager.CreateWindowAsync(options);

    browserWindow.OnReadyToShow += () => browserWindow.Show();
});

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

