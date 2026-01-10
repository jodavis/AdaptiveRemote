using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// UI test service implementation for hosts that use BlazorWebView (WPF/Console) with WebView2.
/// Connects to WebView2 via Playwright using the remote debugging port.
/// </summary>
public class BlazorWebViewUITestService : PlaywrightUITestService
{
    private static readonly TimeSpan InitializePlaywrightTimeout = TimeSpan.FromSeconds(30);

    public BlazorWebViewUITestService(IBrowserDebuggerAccess browserDebugger, ILogger<BlazorWebViewUITestService> logger)
        : base (new BrowserFromPortProvider(browserDebugger, logger))
    {
    }

    private class BrowserFromPortProvider : IBrowserUIAccess, IDisposable
    {
        private readonly IBrowserDebuggerAccess _browserDebugger;
        private readonly ILogger<BlazorWebViewUITestService> _logger;
        private readonly Lazy<IPlaywright> _playwright;
        private readonly Lazy<IBrowser> _browser;

        public BrowserFromPortProvider(IBrowserDebuggerAccess browserDebugger, ILogger<BlazorWebViewUITestService> logger)
        {
            _browserDebugger = browserDebugger;
            _logger = logger;

            _playwright = new(InitializePlaywright);
            _browser = new(ConnectToBrowser);
        }

        private IPlaywright InitializePlaywright()
        {
            using (_logger.LogTimedOperation("Initializing Playwright"))
            {
                return WaitHelpers.WaitForAsyncTask(
                    ct => Playwright.CreateAsync(),
                    InitializePlaywrightTimeout);
            }
        }

        private IBrowser ConnectToBrowser()
        {
            string debuggerUrl = $"http://localhost:{_browserDebugger.Port}";

            using (_logger.LogTimedOperation("Connecting Playwright to Browser"))
            {
                _logger.LogInformation("Connecting to browser debugger at {DebuggerUrl}", debuggerUrl);
                return WaitHelpers.WaitForAsyncTask(
                    ct => _playwright.Value.Chromium.ConnectOverCDPAsync(debuggerUrl),
                    InitializePlaywrightTimeout);
            }
        }

        public object? CurrentPage
        {
            get
            {
                // Get the default context and first page
                IReadOnlyList<IBrowserContext> contexts = _browser.Value.Contexts;
                if (contexts.Count == 0)
                {
                    throw new InvalidOperationException("No browser contexts available in WebView2.");
                }

                IReadOnlyList<IPage> pages = contexts[0].Pages;
                if (pages.Count == 0)
                {
                    throw new InvalidOperationException("No pages available in WebView2 browser context.");
                }

                return pages[0];
            }
        }

        public void Dispose()
        {
            if (_browser.IsValueCreated)
            {
                try
                {
                    _ = _browser.Value.CloseAsync().ConfigureAwait(false);
                }
                catch { }
            }

            if (_playwright.IsValueCreated)
            {
                _playwright.Value.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
