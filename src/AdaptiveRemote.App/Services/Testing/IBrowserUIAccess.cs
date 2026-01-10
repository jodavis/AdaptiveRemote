namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Provides access to browser objects for UI testing.
/// Implemented by hosts that expose their browser for test automation.
/// </summary>
public interface IBrowserUIAccess

{
    /// <summary>
    /// Gets the browser test context for interacting with the UI.
    /// The actual type is Playwright's IPage, but it's returned as object to avoid a direct dependency.
    /// </summary>
    object? CurrentPage { get; }
}
