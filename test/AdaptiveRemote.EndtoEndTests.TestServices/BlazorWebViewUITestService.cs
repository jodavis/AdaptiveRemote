using AdaptiveRemote.Services.Testing;
using Microsoft.Playwright;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// UI test service implementation for hosts that use BlazorWebView (WPF/Console) with WebView2.
/// Connects to WebView2 via Playwright using the remote debugging port.
/// </summary>
public class BlazorWebViewUITestService : IUITestService
{
    private readonly IBrowserProvider _browserProvider;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private const int DefaultTimeoutMs = 2000;
    private const int ClickSettleDelayMs = 100;

    public BlazorWebViewUITestService(IBrowserProvider browserProvider)
    {
        _browserProvider = browserProvider ?? throw new ArgumentNullException(nameof(browserProvider));
    }

    private async Task<IPage> EnsurePageAsync()
    {
        if (_page != null)
        {
            return _page;
        }

        // Get remote debugging port from browser provider
        int remoteDebuggingPort = _browserProvider.BrowserPage as int? ?? throw new InvalidOperationException("Remote debugging port not configured for WebView2.");

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

    public async Task<bool> IsButtonVisibleAsync(string label, CancellationToken cancellationToken = default)
    {
        ILocator locator = await GetButtonLocatorByLabelAsync(label);
        
        try
        {
            return await locator.IsVisibleAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsButtonEnabledAsync(string label, CancellationToken cancellationToken = default)
    {
        ILocator locator = await GetButtonLocatorByLabelAsync(label);
        
        try
        {
            return !await IsButtonDisabledAsync(locator);
        }
        catch
        {
            return false;
        }
    }

    public async Task ClickButtonAsync(string label, CancellationToken cancellationToken = default)
    {
        ILocator locator = await GetButtonLocatorByLabelAsync(label);
        
        // Verify the button is visible
        bool isVisible = await locator.IsVisibleAsync();
        if (!isVisible)
        {
            throw new InvalidOperationException($"Button with label '{label}' is not visible.");
        }

        // Verify the button is enabled
        if (await IsButtonDisabledAsync(locator))
        {
            throw new InvalidOperationException($"Button with label '{label}' is not enabled.");
        }

        // Click the button
        await locator.ClickAsync(new LocatorClickOptions
        {
            Timeout = DefaultTimeoutMs
        });

        // Wait a short time for UI updates to settle
        await Task.Delay(ClickSettleDelayMs, cancellationToken);
    }

    private static async Task<bool> IsButtonDisabledAsync(ILocator locator)
    {
        // Check if the button is disabled via attribute or aria-disabled
        bool hasDisabledAttribute = await locator.GetAttributeAsync("disabled") != null;
        string? ariaDisabled = await locator.GetAttributeAsync("aria-disabled");
        bool isAriaDisabled = ariaDisabled == "true";
        
        return hasDisabledAttribute || isAriaDisabled;
    }

    private async Task<ILocator> GetButtonLocatorByLabelAsync(string label)
    {
        IPage page = await EnsurePageAsync();
        
        // Use Playwright's getByRole with exact match - it will throw meaningful errors
        // if there are no matches or ambiguous matches
        return page.GetByRole(AriaRole.Button, new() { Name = label, Exact = true });
    }

    public void Dispose()
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
