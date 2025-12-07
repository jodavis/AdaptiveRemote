using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// Glue code to set the BlazorWebView's IServiceProvider to the host's service provider.
/// This service will be created and initialized after the IServiceProvider is ready.
/// </summary>
internal class BlazorWindowServicesSetter : IHostedService
{
    private readonly BlazorWebView _browser;
    private readonly IServiceProvider _services;

    public BlazorWindowServicesSetter(MainWindow mainWindow, IServiceProvider services)
    {
        _browser = mainWindow.Browser;
        _services = services;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
        => await _browser.Dispatcher.InvokeAsync(() => _browser.Services = _services);
    
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
