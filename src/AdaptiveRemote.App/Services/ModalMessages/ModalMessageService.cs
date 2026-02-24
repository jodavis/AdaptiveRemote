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
    public ModalMessageView View { get; } = new();

    /// <summary>Initializes the service and starts the background queue processor.</summary>
    public ModalMessageService()
    {
        _ = ProcessQueueAsync();
    }

    /// <inheritdoc/>
    public Task ShowMessageAsync(string message, Func<CancellationToken, Task> body, bool keepAlive = false, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _channel.Writer.TryWrite(new(message, body, keepAlive, tcs, cancellationToken));
        return tcs.Task;
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
