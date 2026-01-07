using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// Provides browser access for WebView2-based hosts.
/// The TestContext property is set to the remote debugging port for use by BlazorWebViewUITestService.
/// </summary>
internal class WebView2BrowserProvider : IBrowserProvider
{
    private readonly TestingSettings _testingSettings;

    public WebView2BrowserProvider(IOptions<TestingSettings> testingSettings)
    {
        _testingSettings = testingSettings.Value;
    }

    /// <summary>
    /// Returns the remote debugging port as an integer (boxed as object).
    /// BlazorWebViewUITestService will unbox this and use it to connect via Playwright.
    /// </summary>
    public object? TestContext => _testingSettings.WebViewRemoteDebuggingPort;
}
