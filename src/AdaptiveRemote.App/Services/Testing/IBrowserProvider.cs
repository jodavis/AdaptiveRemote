namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Provides access to browser objects for UI testing.
/// Implemented by hosts that expose their browser for test automation.
/// </summary>
public interface IBrowserProvider
{
    /// <summary>
    /// Gets the browser page/document instance for interacting with the UI.
    /// The actual type depends on the browser implementation (e.g., Playwright IPage, WebView2 CoreWebView2).
    /// </summary>
    object? BrowserPage { get; }
}
