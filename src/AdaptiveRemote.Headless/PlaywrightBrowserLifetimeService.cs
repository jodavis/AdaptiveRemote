using AdaptiveRemote.Services.Testing;
using AdaptiveRemote.Utilities;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace AdaptiveRemote.Headless;

/// <summary>
/// Hosted service that manages the Playwright browser lifecycle.
/// Launches a headless Chromium browser and navigates to the hosted Blazor app.
/// </summary>
internal class PlaywrightBrowserLifetimeService : BackgroundService, IBrowserUIAccess
{
    private readonly ILogger<PlaywrightBrowserLifetimeService> _logger;
    private readonly IHostApplicationLifetime _lifetime;
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
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _lifetime = lifetime;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for the application to start
        await _lifetime.ApplicationStarted.WaitForCancelledAsync();

        _logger.LogInformation("Starting Playwright hosted service");

        try
        {
            // Get the port the app is listening on (from configuration or default)
            string appUrl = "http://localhost:5000"; // Default

            _logger.LogInformation("Will navigate to: {AppUrl}", appUrl);

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

            _logger.LogInformation("Playwright browser launched");

            _browserContext = await _browser.NewContextAsync();
            if (_settings.TracesDir is not null)
            {
                await _browserContext.Tracing.StartAsync(new TracingStartOptions
                {
                    Screenshots = true,
                    Snapshots = true,
                    Sources = true,
                });
                _logger.LogInformation("Playwright traces will be saved to {TracesDir}", _settings.TracesDir);
            }

            // Create a page
            _page = await _browserContext.NewPageAsync();

            _logger.LogInformation("Playwright page created");

            // Navigate to the Blazor app
            _logger.LogInformation("Navigating to {AppUrl}", appUrl);
            await _page.GotoAsync(appUrl, new()
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = _settings.NavigationTimeout,
            });

            _logger.LogInformation("Playwright browser navigated to Blazor app");

            // Keep running until cancellation is requested
            await stoppingToken.WaitForCancelledAsync();
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
            _logger.LogInformation("Playwright service shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Playwright browser");
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
                _logger.LogInformation("Playwright tracing stopped and saved to {TraceFilePath}", _settings.TracesDir);
                _logger.LogInformation("Go to https://traces.playwright.dev/ to open and view the trace");
            }

            await CleanupAsync();
        }
    }

    private async Task CleanupAsync()
    {
        _logger.LogInformation("Cleaning up Playwright resources");

        try
        {
            if (_page is not null)
            {
                await _page.CloseAsync();
                _page = null;
            }

            if (_browserContext is not null)
            {
                await _browserContext.CloseAsync();
                _browserContext = null;
            }

            if (_browser is not null)
            {
                await _browser.CloseAsync();
                _browser = null;
            }

            _playwright?.Dispose();
            _playwright = null;

            _logger.LogInformation("Playwright resources cleaned up");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while cleaning up Playwright");
        }
    }
}
