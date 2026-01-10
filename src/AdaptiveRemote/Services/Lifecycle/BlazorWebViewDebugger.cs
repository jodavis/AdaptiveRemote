using AdaptiveRemote.Services.Testing;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.Configuration;

namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// Provides browser access for WebView2-based hosts.
/// The TestContext property is set to the remote debugging port for use by BlazorWebViewUITestService.
/// </summary>
internal class BlazorWebViewDebugger : IBrowserDebuggerAccess
{
    public BlazorWebViewDebugger(BlazorWebView blazorWebView, int debuggerPort)
    {
        Port = debuggerPort;

        blazorWebView.BlazorWebViewInitializing += (sender, args) =>
        {
            args.EnvironmentOptions = new()
            {
                AdditionalBrowserArguments = $"--remote-debugging-port={Port}"
            };
        };
    }

    /// <summary>
    /// Returns the remote debugging port as an integer (boxed as object).
    /// BlazorWebViewUITestService will unbox this and use it to connect via Playwright.
    /// </summary>
    public int Port { get; }
}
