using AdaptiveRemote.Services.Testing;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Web.WebView2.Core;

namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// Glue code to set the BlazorWebView's IServiceProvider to the host's service provider,
/// and optionally configure WebView2 for remote debugging if test mode is enabled.
/// </summary>
internal class BlazorWindowServicesSetter : IHostedService
{
    private readonly BlazorWebView _browser;
    private readonly IServiceProvider _services;
    private readonly TestingSettings _testingSettings;

    public BlazorWindowServicesSetter(MainWindow mainWindow, IServiceProvider services, IOptions<TestingSettings> testingSettings)
    {
        _browser = mainWindow.Browser;
        _services = services;
        _testingSettings = testingSettings.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Configure WebView2 for remote debugging if test mode is enabled
        if (_testingSettings.WebViewRemoteDebuggingPort.HasValue)
        {
            await _browser.Dispatcher.InvokeAsync(async () =>
            {
                // Create environment with remote debugging enabled
                var options = new CoreWebView2EnvironmentOptions($"--remote-debugging-port={_testingSettings.WebViewRemoteDebuggingPort.Value}");
                var environment = await CoreWebView2Environment.CreateAsync(null, null, options);
                
                // Ensure the BlazorWebView uses this environment
                await _browser.WebView.EnsureCoreWebView2Async(environment);
            });
        }

        // Set the service provider for the BlazorWebView
        await _browser.Dispatcher.InvokeAsync(() => _browser.Services = _services);
    }
    
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
