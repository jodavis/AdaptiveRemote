using System.Threading.Channels;
using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.ModalMessages;

/// <summary>
/// Implements <see cref="IModalMessageService"/> with a single-consumer FIFO channel
/// so that only one message is visible at a time. The channel processes requests
/// sequentially, ensuring ordering guarantees across concurrent callers.
/// </summary>
internal sealed class ModalMessageService : IModalMessageService, IDisposable
{
    private readonly Channel<MessageRequest> _channel =
        Channel.CreateUnbounded<MessageRequest>(new UnboundedChannelOptions { SingleReader = true });

    /// <inheritdoc/>
    public ModalMessageView View { get; }

    /// <summary>Initializes the service with the given view model and starts the background queue processor.</summary>
    public ModalMessageService(ModalMessageView view)
    {
        View = view;
        _ = ProcessQueueAsync();
    }

    /// <inheritdoc/>
    public async Task ShowMessageAsync(string message, Func<CancellationToken, Task> body, bool keepAlive = false, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await _channel.Writer.WriteAsync(new(message, body, keepAlive, tcs, cancellationToken), cancellationToken);
        await tcs.Task;
    }

    /// <inheritdoc/>
    public void Dispose() => _channel.Writer.Complete();

    private async Task ProcessQueueAsync()
    {
        await foreach (MessageRequest request in _channel.Reader.ReadAllAsync())
        {
            View.CurrentMessage = request.Message;
            try
            {
                await request.Body(request.CancellationToken);

                if (!request.KeepAlive)
                {
                    View.CurrentMessage = null;
                }

                request.Tcs.TrySetResult();
            }
            catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
            {
                View.CurrentMessage = null;
                request.Tcs.TrySetCanceled(request.CancellationToken);
            }
            catch (Exception ex)
            {
                View.CurrentMessage = null;
                request.Tcs.TrySetException(ex);
            }
        }
    }

    private sealed record MessageRequest(
        string Message,
        Func<CancellationToken, Task> Body,
        bool KeepAlive,
        TaskCompletionSource Tcs,
        CancellationToken CancellationToken);
}
