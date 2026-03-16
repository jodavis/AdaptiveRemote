using PolyType;
using StreamJsonRpc;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Interface for UI test services that can interact with the Blazor UI via Playwright.
/// This service allows E2E tests to query UI state and perform interactions.
/// </summary>
[RpcMarshalable]
[JsonRpcContract]
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IUITestService : IDisposable
{
    /// <summary>
    /// Finds a button element in the UI by its visible label text.
    /// </summary>
    /// <param name="label">The exact label text of the button to find (case-sensitive).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// An <see cref="IUIButtonTestObject"/> representing the found button, or <c>null</c> if no matching button is found.
    /// </returns>
    Task<IUIButtonTestObject?> FindButtonByLabelAsync(string label, CancellationToken cancellationToken);

    /// <summary>
    /// Runs an accessibility contrast checker on the current page to detect WCAG violations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of accessibility violations found, or an empty list if none.</returns>
    Task<IReadOnlyList<AccessibilityViolation>> CheckAccessibilityAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Clicks on text with the specified content in the UI.
    /// The text must be visible before clicking.
    /// </summary>
    /// <param name="text">The exact text to click on (case-sensitive).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown if the text is not visible or not clickable.</exception>
    Task ClickTextAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the text content from an element with the specified CSS class.
    /// </summary>
    /// <param name="cssClass">The CSS class name to search for.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The text content of the first matching element, or null if not found or not visible.</returns>
    Task<string?> GetTextFromElementWithCssClassAsync(string cssClass, CancellationToken cancellationToken);
}
