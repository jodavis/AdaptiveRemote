namespace AdaptiveRemote.Headless.Logging;

internal partial class HeadlessHostMessageLogger
{
    private readonly ILogger _logger;

    public HeadlessHostMessageLogger(ILogger logger)
    {
        _logger = logger;
    }

    [LoggerMessage(EventId = 10101, Level = LogLevel.Information, Message = "Circuit {CircuitId} opened.")]
    internal partial void CircuitHandler_Opened(string circuitId);

    [LoggerMessage(EventId = 10102, Level = LogLevel.Information, Message = "Circuit {CircuitId} closed.")]
    internal partial void CircuitHandler_Closed(string circuitId);

    [LoggerMessage(EventId = 10103, Level = LogLevel.Information, Message = "Circuit {CircuitId} connection up.")]
    internal partial void CircuitHandler_ConnectionUp(string circuitId);

    [LoggerMessage(EventId = 10104, Level = LogLevel.Information, Message = "Circuit {CircuitId} connection down.")]
    internal partial void CircuitHandler_ConnectionDown(string circuitId);

    [LoggerMessage(EventId = 10201, Level = LogLevel.Information, Message = "Starting Playwright hosted service.")]
    internal partial void Playwright_StartingHostedService();

    [LoggerMessage(EventId = 10202, Level = LogLevel.Information, Message = "Playwright hosted service shutting down.")]
    internal partial void Playwright_StoppingHostedService();

    [LoggerMessage(EventId = 10203, Level = LogLevel.Information, Message = "Cleaning up Playwright resources.")]
    internal partial void Playwright_CleaningUp();

    [LoggerMessage(EventId = 10204, Level = LogLevel.Information, Message = "Playwright resources cleaned up.")]
    internal partial void Playwright_CleanedUp();

    [LoggerMessage(EventId = 10205, Level = LogLevel.Error, Message = "Error while cleaning up Playwright resources")]
    internal partial void Playwright_ErrorWhileCleaningUp(Exception error);

    [LoggerMessage(EventId = 10206, Level = LogLevel.Information, Message = "Launched Playwright browser.")]
    internal partial void Playwright_BrowserLaunched();

    [LoggerMessage(EventId = 10207, Level = LogLevel.Information, Message = "Created browser page.")]
    internal partial void Playwright_PageCreated();

    [LoggerMessage(EventId = 10208, Level = LogLevel.Information, Message = "Navigating Playwright page to {AppUrl}.")]
    internal partial void Playwright_Navigating(string appUrl);

    [LoggerMessage(EventId = 10209, Level = LogLevel.Information, Message = "Playwright page navigated.")]
    internal partial void Playwright_Navigated();

    [LoggerMessage(EventId = 10210, Level = LogLevel.Error, Message = "Failed to start Playwright browser")]
    internal partial void Playwright_ErrorStartingBrowser(Exception ex);

    [LoggerMessage(EventId = 10211, Level = LogLevel.Information, Message = "Playwright traces will be saved to {TracesDir}")]
    internal partial void Playwright_TraceDirectoryConfigured(string tracesDir);

    [LoggerMessage(EventId = 10212, Level = LogLevel.Information, Message = "Playwright tracing stopped and saved to {TraceFilePath}\n\tGo to https://traces.playwright.dev/ to open and view the trace")]
    internal partial void Playwright_TracesSaved(string traceFilePath);
}
