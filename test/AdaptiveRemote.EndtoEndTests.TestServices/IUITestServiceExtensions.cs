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
        try
        {
            bool succeeded = WaitHelpers.WaitForAsyncTask(ct => service.ClickButtonAsync(label, ct), timeout);
            if (!succeeded)
            {
                throw new TimeoutException($"Clicking button '{label}' did not complete within timeout.");
            }
        }
        catch (AggregateException ex) when (label.Equals("Exit", StringComparison.OrdinalIgnoreCase) && ex.InnerException is StreamJsonRpc.ConnectionLostException)
        {
            // This exception occurs sometimes when clicking the "Exit" button if the application shuts down to fast
        }
    }

    /// <summary>
    /// Clicks on text with the specified content in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="text">The exact text to click on (case-sensitive).</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    /// <exception cref="TimeoutException">Thrown when the operation times out.</exception>
    public static void ClickText(this IUITestService service, string text, int timeoutInSeconds = DefaultUITimeoutInSeconds)
        => service.ClickText(text, TimeSpan.FromSeconds(timeoutInSeconds));

    /// <summary>
    /// Clicks on text with the specified content in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="text">The exact text to click on (case-sensitive).</param>
    /// <param name="timeout">Timeout for the operation.</param>
    /// <exception cref="TimeoutException">Thrown when the operation times out.</exception>
    public static void ClickText(this IUITestService service, string text, TimeSpan timeout)
    {
        bool succeeded = WaitHelpers.WaitForAsyncTask(ct => service.ClickTextAsync(text, ct), timeout);
        if (!succeeded)
        {
            throw new TimeoutException($"Clicking text '{text}' did not complete within timeout.");
        }
    }

    /// <summary>
    /// Waits for a modal message containing the specified text to appear.
    /// This uses polling with retries to handle timing issues with speech synthesis.
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="expectedText">The expected text content in the modal message.</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation (default 5 seconds).</param>
    /// <exception cref="TimeoutException">Thrown when the modal doesn't appear or has different text within the timeout.</exception>
    public static void WaitForModalMessageContaining(this IUITestService service, string expectedText, int timeoutInSeconds = DefaultUITimeoutInSeconds)
    {
        const string modalCssClass = "conversation-speaking-message";
        string? actualText = null;

        bool found = WaitHelpers.ExecuteWithRetries(() =>
        {
            actualText = WaitHelpers.WaitForAsyncTask(
                ct => service.GetTextFromElementWithCssClassAsync(modalCssClass, ct),
                timeoutInSeconds);

            return actualText != null && actualText.Contains(expectedText, StringComparison.OrdinalIgnoreCase);
        }, timeoutInSeconds);

        if (!found)
        {
            if (actualText == null)
            {
                throw new TimeoutException($"Modal message box did not appear within {timeoutInSeconds} seconds.");
            }
            else
            {
                throw new TimeoutException($"Modal message box appeared but contained '{actualText}' instead of expected text '{expectedText}'.");
            }
        }
    }
}
