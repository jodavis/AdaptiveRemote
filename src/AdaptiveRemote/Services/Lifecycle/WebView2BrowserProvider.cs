using AdaptiveRemote.Services.Testing;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.Configuration;

namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// Provides browser access for WebView2-based hosts.
/// The BrowserPage property is set to the remote debugging port for use by BlazorWebViewUITestService.
/// </summary>
internal class WebView2BrowserProvider : IBrowserProvider
{
    private readonly IConfiguration _configuration;

    public WebView2BrowserProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Returns the remote debugging port as an integer (boxed as object).
    /// BlazorWebViewUITestService will unbox this and use it to connect via Playwright.
    /// </summary>
    public object? BrowserPage => _configuration.GetValue<int?>("test:WebViewRemoteDebuggingPort");
}
