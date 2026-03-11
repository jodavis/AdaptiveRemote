using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services;

/// <summary>
/// Service for displaying modal messages to the user with FIFO queuing support.
/// Messages are queued so that only one is visible at a time. Used for both
/// conversation (speech synthesis) and programming (IR learning) messages.
/// </summary>
/// <remarks>
/// See <c>_doc_ModalMessages.md</c> for design details and queuing requirements.
/// </remarks>
public interface IModalMessageService
{
    /// <summary>Gets the view model that reflects the currently displayed message.</summary>
    ModalMessageView View { get; }

    /// <summary>
    /// Shows a message while an async body runs, queuing if another message is already displayed.
    /// The returned task completes when the body finishes. If <paramref name="keepAlive"/> is
    /// <see langword="true"/>, the message remains visible after the body completes and is cleared
    /// only when the next message arrives.
    /// </summary>
    /// <param name="message">The message to display (may contain markdown).</param>
    /// <param name="body">Async work to perform while the message is visible.</param>
    /// <param name="keepAlive">
    /// When <see langword="true"/>, the message stays visible after <paramref name="body"/> completes
    /// until the next call to <see cref="ShowMessageAsync"/> replaces it.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel waiting for a queue slot.</param>
    Task ShowMessageAsync(string message, Func<CancellationToken, Task> body, bool keepAlive = false, CancellationToken cancellationToken = default);
}
