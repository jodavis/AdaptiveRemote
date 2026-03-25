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
    private readonly TcpListener _tcpListener;

    public BlazorWebViewDebugger(BlazorWebView blazorWebView)
    {
        _tcpListener = new(System.Net.IPAddress.Loopback, 0);
        _tcpListener.Start();
        Port = ((System.Net.IPEndPoint)_tcpListener.LocalEndpoint).Port;

        blazorWebView.BlazorWebViewInitializing += (sender, args) =>
        {
            // Stop the listener as soon as the port is assigned to free it up for Playwright to use.
            _tcpListener.Stop();
            _tcpListener.Dispose();

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
