using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Web.WebView2.Core;

namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// Configures the WebView2 environment for testing if remote debugging is enabled.
/// Must run before BlazorWindowServicesSetter to ensure the environment is configured before the WebView is initialized.
/// </summary>
internal class WebView2TestConfigurator : IHostedService
{
    private readonly BlazorWebView _browser;
    private readonly IConfiguration _configuration;

    public WebView2TestConfigurator(MainWindow mainWindow, IConfiguration configuration)
    {
        _browser = mainWindow.Browser;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        int? remoteDebuggingPort = _configuration.GetValue<int?>("test:WebViewRemoteDebuggingPort");
        
        if (remoteDebuggingPort.HasValue)
        {
            await _browser.Dispatcher.InvokeAsync(async () =>
            {
                // Create per-instance user data folder to avoid collisions
                string userDataFolder = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), 
                    "AdaptiveRemote_WebView2", 
                    Environment.ProcessId.ToString(), 
                    remoteDebuggingPort.Value.ToString());
                
                System.IO.Directory.CreateDirectory(userDataFolder);
                
                // Create environment with remote debugging enabled
                var options = new CoreWebView2EnvironmentOptions($"--remote-debugging-port={remoteDebuggingPort.Value}");
                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                
                // Ensure the BlazorWebView uses this environment
                await _browser.WebView.EnsureCoreWebView2Async(environment);
            });
        }
    }
    
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
