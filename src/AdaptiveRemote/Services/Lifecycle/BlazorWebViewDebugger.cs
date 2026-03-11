using System.Net.Sockets;
using AdaptiveRemote.Services.Testing;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// Provides browser access for WebView2-based hosts.
/// The TestContext property is set to the remote debugging port for use by BlazorWebViewUITestService.
/// </summary>
internal class BlazorWebViewDebugger : IBrowserDebuggerAccess
{
    public BlazorWebViewDebugger(BlazorWebView blazorWebView)
    {
        Port = 0;

        blazorWebView.BlazorWebViewInitializing += (sender, args) =>
        {
            Port = GetAvailablePort();
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
    public int Port { get; private set; }

    private static int GetAvailablePort()
    {
        using TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
