using AdaptiveRemote.Services.Testing;
using Microsoft.Playwright;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// UI test service implementation for hosts that use BlazorWebView (WPF/Console) with WebView2.
/// Connects to WebView2 via Playwright using the remote debugging port.
/// </summary>
public class BlazorWebViewUITestService : UITestServiceBase
{
    private readonly IBrowserProvider _browserProvider;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public BlazorWebViewUITestService(IBrowserProvider browserProvider)
    {
        _browserProvider = browserProvider ?? throw new ArgumentNullException(nameof(browserProvider));
    }

    protected override async Task<IPage> GetPageAsync()
    {
        if (_page != null)
        {
            return _page;
        }

        // Get remote debugging port from browser provider
        int remoteDebuggingPort = _browserProvider.TestContext as int? ?? throw new InvalidOperationException("Remote debugging port not configured for WebView2.");

        // Initialize Playwright and connect to WebView2
        _playwright = await Playwright.CreateAsync();
        
        // Connect to the WebView2 instance via remote debugging protocol
        string cdpUrl = $"http://localhost:{remoteDebuggingPort}";
        _browser = await _playwright.Chromium.ConnectOverCDPAsync(cdpUrl);
        
        // Get the default context and first page
        var contexts = _browser.Contexts;
        if (contexts.Count == 0)
        {
            throw new InvalidOperationException("No browser contexts available in WebView2.");
        }
        
        var pages = contexts[0].Pages;
        if (pages.Count == 0)
        {
            throw new InvalidOperationException("No pages available in WebView2 browser context.");
        }
        
        _page = pages[0];
        return _page;
    }

    public override void Dispose()
    {
        // Clean up Playwright resources
        _page = null;
        
        if (_browser != null)
        {
            try
            {
                _ = _browser.CloseAsync().ConfigureAwait(false);
            }
            catch { }
        }
        
        _playwright?.Dispose();
        GC.SuppressFinalize(this);
    }
}
