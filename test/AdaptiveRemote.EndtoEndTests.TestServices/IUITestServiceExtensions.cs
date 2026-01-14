using AdaptiveRemote.Services.Testing;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Synchronous wrapper extensions for IUITestService to simplify test code.
/// </summary>
public static class IUITestServiceExtensions
{
    public const int DefaultUITimeoutInSeconds = 60;

    /// <summary>
    /// Checks if a button with the specified label is visible in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="label">The exact visible text of the button (case-sensitive, trimmed).</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    /// <returns>True if the button is visible, false otherwise.</returns>
    public static bool IsButtonVisible(this IUITestService service, string label, int timeoutInSeconds = DefaultUITimeoutInSeconds)
        => service.IsButtonVisible(label, TimeSpan.FromSeconds(timeoutInSeconds));

    /// <summary>
    /// Checks if a button with the specified label is visible in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="label">The exact visible text of the button (case-sensitive, trimmed).</param>
    /// <param name="timeout">Timeout for the operation.</param>
    /// <returns>True if the button is visible, false otherwise.</returns>
    public static bool IsButtonVisible(this IUITestService service, string label, TimeSpan timeout)
        => WaitHelpers.ExecuteWithRetries(ct => service.IsButtonVisibleAsync(label, ct), timeout);

    /// <summary>
    /// Checks if a button with the specified label is enabled in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="label">The exact visible text of the button (case-sensitive, trimmed).</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    /// <returns>True if the button is enabled, false otherwise.</returns>
    public static bool IsButtonEnabled(this IUITestService service, string label, int timeoutInSeconds = DefaultUITimeoutInSeconds)
        => service.IsButtonEnabled(label, TimeSpan.FromSeconds(timeoutInSeconds));

    /// <summary>
    /// Checks if a button with the specified label is enabled in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="label">The exact visible text of the button (case-sensitive, trimmed).</param>
    /// <param name="timeout">Timeout for the operation.</param>
    /// <returns>True if the button is enabled, false otherwise.</returns>
    public static bool IsButtonEnabled(this IUITestService service, string label, TimeSpan timeout)
        => WaitHelpers.ExecuteWithRetries(ct => service.IsButtonEnabledAsync(label, ct), timeout);

    /// <summary>
    /// Clicks a button with the specified label in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="label">The exact visible text of the button (case-sensitive, trimmed).</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    /// <exception cref="TimeoutException">Thrown when the operation times out.</exception>
    public static void ClickButton(this IUITestService service, string label, int timeoutInSeconds = DefaultUITimeoutInSeconds)
        => service.ClickButton(label, TimeSpan.FromSeconds(timeoutInSeconds));

    /// <summary>
    /// Clicks a button with the specified label in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="label">The exact visible text of the button (case-sensitive, trimmed).</param>
    /// <param name="timeout">Timeout for the operation.</param>
    /// <exception cref="TimeoutException">Thrown when the operation times out.</exception>
    public static void ClickButton(this IUITestService service, string label, TimeSpan timeout)
    {
        bool succeeded = WaitHelpers.WaitForAsyncTask(ct => service.ClickButtonAsync(label, ct));
        if (!succeeded)
        {
            throw new TimeoutException($"Clicking button '{label}' did not complete within timeout.");
        }
    }
}
