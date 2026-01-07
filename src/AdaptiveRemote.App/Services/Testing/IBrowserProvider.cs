namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Provides access to browser objects for UI testing.
/// Implemented by hosts that expose their browser for test automation.
/// </summary>
public interface IBrowserProvider
{
    /// <summary>
    /// Gets the browser test context for interacting with the UI.
    /// The actual type depends on the browser implementation:
    /// - For Playwright-based hosts: IPage instance
    /// - For WebView2-based hosts: Remote debugging port (int)
    /// </summary>
    object? TestContext { get; }
}
