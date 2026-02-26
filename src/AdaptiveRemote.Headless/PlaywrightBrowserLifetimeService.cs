using AdaptiveRemote.Headless.Logging;
using AdaptiveRemote.Services.Testing;
using AdaptiveRemote.Utilities;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace AdaptiveRemote.Headless;

/// <summary>
/// Hosted service that manages the Playwright browser lifecycle.
/// Launches a headless Chromium browser and navigates to the hosted Blazor app.
/// </summary>
internal class PlaywrightBrowserLifetimeService : BackgroundService, IBrowserUIAccess
{
    private readonly HeadlessHostMessageLogger _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServer _server;
    private readonly PlaywrightSettings _settings;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _browserContext;
    private IPage? _page;

    /// <summary>
    /// Gets the browser page for UI testing as a generic object.
    /// </summary>
    object? IBrowserUIAccess.CurrentPage => _page;

    public PlaywrightBrowserLifetimeService(
        ILogger<PlaywrightBrowserLifetimeService> logger,
        IOptions<PlaywrightSettings> options,
        IHostApplicationLifetime lifetime,
        IServer server)
    {
        _logger = new(logger);
        _lifetime = lifetime;
        _server = server;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for the application to start
        await _lifetime.ApplicationStarted.WaitForCancelledAsync();

        _logger.Playwright_StartingHostedService();

        try
        {
            // Get the actual URL the server is listening on
            IServerAddressesFeature? serverAddressesFeature = _server.Features.Get<IServerAddressesFeature>();
            string appUrl = serverAddressesFeature?.Addresses.FirstOrDefault() ?? "http://localhost:5000";

            // Initialize Playwright (browsers should be installed during build or first run)
            _playwright = await Playwright.CreateAsync();

            // Launch headless browser
            BrowserTypeLaunchOptions launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = _settings.Headless,
                Args = ["--no-sandbox", "--disable-dev-shm-usage"],
                TracesDir = _settings.TracesDir,
            };

            _browser = await _playwright.Chromium.LaunchAsync(launchOptions);

            _logger.Playwright_BrowserLaunched();

            _browserContext = await _browser.NewContextAsync();
            if (_settings.TracesDir is not null)
            {
                await _browserContext.Tracing.StartAsync(new TracingStartOptions
                {
                    Screenshots = true,
                    Snapshots = true,
                    Sources = true,
                });
                _logger.Playwright_TraceDirectoryConfigured(_settings.TracesDir);
            }

            // Create a page
            _page = await _browserContext.NewPageAsync();

            _logger.Playwright_PageCreated();

            // Navigate to the Blazor app
            _logger.Playwright_Navigating(appUrl);
            await _page.GotoAsync(appUrl, new()
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = _settings.NavigationTimeout,
            });

            _logger.Playwright_Navigated();

            // Keep running until cancellation is requested
            await stoppingToken.WaitForCancelledAsync();
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
            _logger.Playwright_StoppingHostedService();
        }
        catch (Exception ex)
        {
            _logger.Playwright_ErrorStartingBrowser(ex);
            throw;
        }
        finally
        {
            if (_browserContext is not null && _settings.TracesDir is not null)
            {
                await _browserContext.Tracing.StopAsync(new TracingStopOptions
                {
                    Path = Path.Combine(_settings.TracesDir, "traces.zip"),
                });
                _logger.Playwright_TracesSaved(_settings.TracesDir);
            }

            await CleanupAsync();
        }
    }

    private async Task CleanupAsync()
    {
        _logger.Playwright_CleaningUp();

        try
        {
            if (_page is not null)
            {
                _logger.Playwright_ClosingPage();
                await _page.CloseAsync();
                _page = null;
                _logger.Playwright_PageClosed();
            }

            if (_browserContext is not null)
            {
                _logger.Playwright_ClosingBrowserContext();
                await _browserContext.CloseAsync();
                _browserContext = null;
                _logger.Playwright_BrowserContextClosed();
            }

            if (_browser is not null)
            {
                _logger.Playwright_ClosingBrowser();
                await _browser.CloseAsync();
                _browser = null;
                _logger.Playwright_BrowserClosed();
            }

            _logger.Playwright_DisposingDriver();
            _playwright?.Dispose();
            _playwright = null;
            _logger.Playwright_DriverDisposed();

            _logger.Playwright_CleanedUp();
        }
        catch (Exception ex)
        {
            _logger.Playwright_ErrorWhileCleaningUp(ex);
        }
    }
}
