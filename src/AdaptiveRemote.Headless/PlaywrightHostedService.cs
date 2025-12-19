using Microsoft.Playwright;

namespace AdaptiveRemote.Headless;

/// <summary>
/// Hosted service that manages the Playwright browser lifecycle.
/// Launches a headless Chromium browser and navigates to the hosted Blazor app.
/// </summary>
internal class PlaywrightHostedService : BackgroundService
{
    private readonly ILogger<PlaywrightHostedService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _lifetime;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public PlaywrightHostedService(
        ILogger<PlaywrightHostedService> logger,
        IConfiguration configuration,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _configuration = configuration;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for the application to start
        var tcs = new TaskCompletionSource();
        using var reg = _lifetime.ApplicationStarted.Register(() => tcs.SetResult());
        await tcs.Task;

        _logger.LogInformation("Starting Playwright hosted service");

        try
        {
            // Get the port the app is listening on (from configuration or default)
            string? urls = _configuration["ASPNETCORE_URLS"] ?? _configuration["urls"];
            string appUrl = "http://localhost:5000"; // Default

            if (!string.IsNullOrEmpty(urls))
            {
                // Parse the first URL from the list
                var urlList = urls.Split(';');
                if (urlList.Length > 0)
                {
                    appUrl = urlList[0];
                }
            }

            _logger.LogInformation("Will navigate to: {AppUrl}", appUrl);

            // Initialize Playwright (browsers should be installed during build or first run)
            _playwright = await Playwright.CreateAsync();

            // Launch headless browser
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" }
            };
            
            _browser = await _playwright.Chromium.LaunchAsync(launchOptions);

            _logger.LogInformation("Playwright browser launched");

            // Create a page
            _page = await _browser.NewPageAsync();

            _logger.LogInformation("Playwright page created");

            // Wait a moment for the ASP.NET server to fully initialize
            await Task.Delay(1000, stoppingToken);

            // Navigate to the Blazor app
            _logger.LogInformation("Navigating to {AppUrl}", appUrl);
            await _page.GotoAsync(appUrl, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });

            _logger.LogInformation("Playwright browser navigated to Blazor app");

            // Keep running until cancelled
            await Task.Delay(Timeout.Infinite, stoppingToken);
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
            await CleanupAsync();
        }
    }

    private async Task CleanupAsync()
    {
        _logger.LogInformation("Cleaning up Playwright resources");

        try
        {
            if (_page != null)
            {
                await _page.CloseAsync();
                _page = null;
            }

            if (_browser != null)
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

    /// <summary>
    /// Gets the Playwright page for browser interaction (for testing).
    /// </summary>
    public IPage? Page => _page;
}
