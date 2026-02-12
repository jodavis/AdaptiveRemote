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
    /// Checks if text content is visible in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="text">The text to search for (case-sensitive).</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    /// <returns>True if the text is visible anywhere in the UI, false otherwise.</returns>
    public static bool IsTextVisible(this IUITestService service, string text, int timeoutInSeconds = DefaultUITimeoutInSeconds)
        => service.IsTextVisible(text, TimeSpan.FromSeconds(timeoutInSeconds));

    /// <summary>
    /// Checks if text content is visible in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="text">The text to search for (case-sensitive).</param>
    /// <param name="timeout">Timeout for the operation.</param>
    /// <returns>True if the text is visible anywhere in the UI, false otherwise.</returns>
    public static bool IsTextVisible(this IUITestService service, string text, TimeSpan timeout)
        => WaitHelpers.ExecuteWithRetries(ct => service.IsTextVisibleAsync(text, ct), timeout);

    /// <summary>
    /// Clicks on an element containing the specified text in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="text">The exact text content to find and click (case-sensitive).</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    /// <exception cref="TimeoutException">Thrown when the operation times out.</exception>
    public static void ClickText(this IUITestService service, string text, int timeoutInSeconds = DefaultUITimeoutInSeconds)
        => service.ClickText(text, TimeSpan.FromSeconds(timeoutInSeconds));

    /// <summary>
    /// Clicks on an element containing the specified text in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="text">The exact text content to find and click (case-sensitive).</param>
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
    /// Gets the text content from the conversation speaking message div (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    /// <returns>The speaking message text if visible, otherwise null.</returns>
    public static string? GetSpeakingMessage(this IUITestService service, int timeoutInSeconds = DefaultUITimeoutInSeconds)
        => service.GetSpeakingMessage(TimeSpan.FromSeconds(timeoutInSeconds));

    /// <summary>
    /// Gets the text content from the conversation speaking message div (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="timeout">Timeout for the operation.</param>
    /// <returns>The speaking message text if visible, otherwise null.</returns>
    public static string? GetSpeakingMessage(this IUITestService service, TimeSpan timeout)
        => WaitHelpers.WaitForAsyncTask(service.GetSpeakingMessageAsync, timeout);

    /// <summary>
    /// Waits for the speaking message to match the expected text.
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="expectedMessage">The expected message text.</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    /// <returns>True if the message appears within the timeout, false otherwise.</returns>
    public static bool WaitForSpeakingMessage(this IUITestService service, string expectedMessage, int timeoutInSeconds = DefaultUITimeoutInSeconds)
        => service.WaitForSpeakingMessage(expectedMessage, TimeSpan.FromSeconds(timeoutInSeconds));

    /// <summary>
    /// Waits for the speaking message to match the expected text.
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="expectedMessage">The expected message text.</param>
    /// <param name="timeout">Timeout for the operation.</param>
    /// <returns>True if the message appears within the timeout, false otherwise.</returns>
    public static bool WaitForSpeakingMessage(this IUITestService service, string expectedMessage, TimeSpan timeout)
    {
        return WaitHelpers.ExecuteWithRetries(async ct =>
        {
            string? actualMessage = await service.GetSpeakingMessageAsync(ct);
            return actualMessage == expectedMessage;
        }, timeout);
    }
}
