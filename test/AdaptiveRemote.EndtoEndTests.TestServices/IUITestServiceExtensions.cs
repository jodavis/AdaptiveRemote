using System.Diagnostics.CodeAnalysis;
using AdaptiveRemote.Services.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Synchronous wrapper extensions for IUITestService to simplify test code.
/// </summary>
public static class IUITestServiceExtensions
{
    public const int DefaultUITimeoutInSeconds = 5;

    /// <summary>
    /// Waits for a button with the specified label to be visible or not visible in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="label">The exact visible text of the button (case-sensitive, trimmed).</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    /// <returns>True if the button is visible, false otherwise.</returns>
    public static bool WaitForButtonVisible(this IUITestService service, string label, bool visible = true, int timeoutInSeconds = DefaultUITimeoutInSeconds)
        => service.WaitForButtonVisible(label, visible, TimeSpan.FromSeconds(timeoutInSeconds));

    /// <summary>
    /// Waits for a button with the specified label to be visible or not visible in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="label">The exact visible text of the button (case-sensitive, trimmed).</param>
    /// <param name="timeout">Timeout for the operation.</param>
    /// <returns>True if the button is visible, false otherwise.</returns>
    public static bool WaitForButtonVisible(this IUITestService service, string label, bool visible, TimeSpan timeout)
    {
        using IUIButtonTestObject button = service.GetButtonByLabel(label, timeout);

        return WaitHelpers.WaitForState(button.IsVisibleAsync, visible, timeout);
    }

    /// <summary>
    /// Waits for a button with the specified label to be enabled or disabled in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="label">The exact visible text of the button (case-sensitive, trimmed).</param>
    /// <param name="enabled">Whether to wait for the button to be enabled or disabled.</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    /// <returns>True if the button reaches the desired state within the timeout, false otherwise.</returns>
    public static bool WaitForButtonEnabled(this IUITestService service, string label, bool enabled = true, int timeoutInSeconds = DefaultUITimeoutInSeconds)
        => service.WaitForButtonEnabled(label, enabled, TimeSpan.FromSeconds(timeoutInSeconds));

    /// <summary>
    /// Waits for a button with the specified label to be enabled or disabled in the UI (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="label">The exact visible text of the button (case-sensitive, trimmed).</param>
    /// <param name="enabled">Whether to wait for the button to be enabled or disabled.</param>
    /// <param name="timeout">Timeout for the operation.</param>
    /// <returns>True if the button reaches the desired state within the timeout, false otherwise.</returns>
    public static bool WaitForButtonEnabled(this IUITestService service, string label, bool enabled, TimeSpan timeout)
    {
        using IUIButtonTestObject button = service.GetButtonByLabel(label, timeout);

        return WaitHelpers.WaitForState(button.IsEnabledAsync, enabled, timeout);
    }

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
            using IUIButtonTestObject button = service.GetButtonByLabel(label, timeout);
            
            bool succeeded = WaitHelpers.WaitForAsyncTask(button.ClickAsync, timeout);
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
    /// Runs an accessibility contrast checker on the current page (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    /// <returns>A list of accessibility violations found, or an empty list if none.</returns>
    public static IReadOnlyList<AccessibilityViolation> CheckAccessibility(this IUITestService service, int timeoutInSeconds = DefaultUITimeoutInSeconds)
        => service.CheckAccessibility(TimeSpan.FromSeconds(timeoutInSeconds));

    /// <summary>
    /// Runs an accessibility contrast checker on the current page (synchronous wrapper).
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="timeout">Timeout for the operation.</param>
    /// <returns>A list of accessibility violations found, or an empty list if none.</returns>
    public static IReadOnlyList<AccessibilityViolation> CheckAccessibility(this IUITestService service, TimeSpan timeout)
        => WaitHelpers.WaitForAsyncTask(service.CheckAccessibilityAsync, timeout);

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

    /// <summary>
    /// Waits until no modal message is visible in the UI.
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    /// <exception cref="TimeoutException">Thrown when a modal message is still visible after the timeout.</exception>
    public static void WaitForNoModalMessage(this IUITestService service, int timeoutInSeconds = DefaultUITimeoutInSeconds)
    {
        const string modalCssClass = "conversation-speaking-message";

        bool dismissed = WaitHelpers.ExecuteWithRetries(() =>
        {
            string? text = WaitHelpers.WaitForAsyncTask(
                ct => service.GetTextFromElementWithCssClassAsync(modalCssClass, ct),
                timeoutInSeconds);

            return text is null;
        }, timeoutInSeconds);

        if (!dismissed)
        {
            throw new TimeoutException($"Modal message was still visible after {timeoutInSeconds} seconds.");
        }
    }

    /// <summary>
    /// Waits for the modal message element's inner HTML to contain the expected markup.
    /// Use this to verify that markdown was rendered correctly as HTML.
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="expectedMarkup">A substring of HTML markup expected to appear in the modal element's inner HTML.</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation (default 5 seconds).</param>
    /// <exception cref="TimeoutException">Thrown when the modal doesn't appear or its markup doesn't contain the expected value within the timeout.</exception>
    public static void WaitForModalMessageMarkupContaining(this IUITestService service, string expectedMarkup, int timeoutInSeconds = DefaultUITimeoutInSeconds)
    {
        const string modalCssClass = "conversation-speaking-message";
        string? actualHtml = null;

        expectedMarkup = NormalizeHtml(expectedMarkup);

        bool found = WaitHelpers.ExecuteWithRetries(() =>
        {
            string? nextHtml = WaitHelpers.WaitForAsyncTask(
                ct => service.GetInnerHtmlFromElementWithCssClassAsync(modalCssClass, ct),
                timeoutInSeconds);
            actualHtml = NormalizeHtml(nextHtml) ?? actualHtml;

            return actualHtml != null && actualHtml.Contains(expectedMarkup, StringComparison.OrdinalIgnoreCase);
        }, timeoutInSeconds);

        if (!found)
        {
            if (actualHtml == null)
            {
                throw new TimeoutException($"Modal message box did not appear within {timeoutInSeconds} seconds.");
            }
            else
            {
                throw new TimeoutException($"Modal message box appeared but its HTML was '{actualHtml}' instead of containing expected markup '{expectedMarkup}'.");
            }
        }

        [return:NotNullIfNotNull(nameof(html))]
        static string? NormalizeHtml(string? html)
        {
            return html?.Replace("\r", "").Replace("\n", "").Replace("\t", "").Trim();
        }
    }

    /// <summary>
    /// Waits until the button with the given label reaches the desired programmed state.
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="label">The exact visible text of the button.</param>
    /// <param name="programmed">Whether to wait for programmed (<c>true</c>) or unprogrammed (<c>false</c>) state.</param>
    /// <param name="timeoutInSeconds">Optional timeout for the operation.</param>
    /// <returns>True if the button reaches the desired state within the timeout; false otherwise.</returns>
    public static bool WaitForButtonProgrammed(this IUITestService service, string label, bool programmed = true, int timeoutInSeconds = DefaultUITimeoutInSeconds)
        => service.WaitForButtonProgrammed(label, programmed, TimeSpan.FromSeconds(timeoutInSeconds));

    /// <summary>
    /// Waits until the button with the given label reaches the desired programmed state.
    /// </summary>
    /// <param name="service">The UI test service.</param>
    /// <param name="label">The exact visible text of the button.</param>
    /// <param name="programmed">Whether to wait for programmed (<c>true</c>) or unprogrammed (<c>false</c>) state.</param>
    /// <param name="timeout">Timeout for the operation.</param>
    /// <returns>True if the button reaches the desired state within the timeout; false otherwise.</returns>
    public static bool WaitForButtonProgrammed(this IUITestService service, string label, bool programmed, TimeSpan timeout)
    {
        using IUIButtonTestObject button = service.GetButtonByLabel(label, timeout);

        return WaitHelpers.WaitForState(button.IsProgrammedAsync, programmed, timeout);
    }

    private static IUIButtonTestObject GetButtonByLabel(this IUITestService service, string label, TimeSpan timeout)
    {
        IUIButtonTestObject? button = null;
        WaitHelpers.ExecuteWithRetries(async ct =>
        {
            button = await service.FindButtonByLabelAsync(label, ct);
            return button is not null;
        }, timeout);

        Assert.IsNotNull(button, "Button with label '{0}' was not found. (Waited {1}s)", label, timeout.TotalSeconds);

        return button;
    }
}
