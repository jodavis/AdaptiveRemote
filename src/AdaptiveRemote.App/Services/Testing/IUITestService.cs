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
    /// Checks if a button with the specified label is visible in the UI.
    /// </summary>
    /// <param name="label">The exact visible text of the button (case-sensitive, trimmed).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the button is visible, false otherwise.</returns>
    /// <exception cref="InvalidOperationException">Thrown if multiple buttons match the label.</exception>
    Task<bool> IsButtonVisibleAsync(string label, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if a button with the specified label is enabled in the UI.
    /// </summary>
    /// <param name="label">The exact visible text of the button (case-sensitive, trimmed).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the button is enabled, false otherwise.</returns>
    /// <exception cref="InvalidOperationException">Thrown if multiple buttons match the label.</exception>
    Task<bool> IsButtonEnabledAsync(string label, CancellationToken cancellationToken);

    /// <summary>
    /// Clicks a button with the specified label in the UI.
    /// The button must be visible and enabled before clicking.
    /// </summary>
    /// <param name="label">The exact visible text of the button (case-sensitive, trimmed).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown if multiple buttons match the label or if the button is not visible/enabled.</exception>
    Task ClickButtonAsync(string label, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if text content is visible in the UI.
    /// </summary>
    /// <param name="text">The text to search for (case-sensitive).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the text is visible anywhere in the UI, false otherwise.</returns>
    Task<bool> IsTextVisibleAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Clicks on an element containing the specified text in the UI.
    /// The element must be visible and clickable.
    /// </summary>
    /// <param name="text">The exact text content to find and click (case-sensitive).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown if the text is not found or not clickable.</exception>
    Task ClickTextAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the text content from the conversation speaking message div, if visible.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The speaking message text if visible, otherwise null.</returns>
    Task<string?> GetSpeakingMessageAsync(CancellationToken cancellationToken);
}
