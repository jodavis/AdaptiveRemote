using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AdaptiveRemote.TestUtilities;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests.SimulatedTiVo;

/// <summary>
/// Simulates a TiVo device for E2E testing.
/// Accepts TCP connections and records messages according to the TiVo protocol.
/// </summary>
public sealed class SimulatedTiVoDevice : ISimulatedTiVoDevice
{
    private readonly ILogger _logger;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ConcurrentBag<RecordedMessage> _recordedMessages = new();
    private readonly Task _listenerTask;
    private bool _disposed;

    internal SimulatedTiVoDevice(int port, ILogger logger)
    {
        _logger = logger;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _logger.LogInformation("SimulatedTiVoDevice started on port {Port}", Port);

        _listenerTask = Task.Run(() => AcceptConnectionsAsync(_cancellationTokenSource.Token));
    }

    /// <inheritdoc/>
    public int Port { get; }

    /// <inheritdoc/>
    public IReadOnlyList<RecordedMessage> GetRecordedMessages()
    {
        return _recordedMessages.ToList();
    }

    /// <inheritdoc/>
    public void ClearRecordedMessages()
    {
        // Create a new bag instead of clearing the existing one for thread safety
        while (_recordedMessages.TryTake(out _))
        {
            // Remove all items
        }

        _logger.LogInformation("Cleared recorded messages");
    }

    /// <inheritdoc/>
    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Stopping SimulatedTiVoDevice on port {Port}", Port);

        _cancellationTokenSource.Cancel();
        _listener.Stop();

        try
        {
            WaitHelpers.WaitForAsyncTask(_ => _listenerTask, TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error waiting for listener task to complete");
        }

        _disposed = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
        _cancellationTokenSource.Dispose();
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Use CancellationToken.None to explicitly indicate we handle cancellation via the while loop condition
                TcpClient client = await _listener.AcceptTcpClientAsync(CancellationToken.None);
                _logger.LogInformation("Accepted connection from {RemoteEndPoint}", client.Client.RemoteEndPoint);

                // Handle client in background
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting connection");
                break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new(stream, Encoding.ASCII))
            {
                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    string? line = await ReadLineAsync(reader, cancellationToken);

                    if (line is null)
                    {
                        break;
                    }

                    RecordedMessage message = new RecordedMessage
                    {
                        Timestamp = DateTimeOffset.UtcNow,
                        Payload = line,
                        Incoming = true
                    };

                    _recordedMessages.Add(message);
                    _logger.LogInformation("Received message: {Payload}", line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client connection");
        }
        finally
        {
            _logger.LogInformation("Client disconnected");
        }
    }

    private static async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        StringBuilder line = new();

        while (!cancellationToken.IsCancellationRequested)
        {
            // Use ReadAsync with a buffer instead of Task.Run with synchronous Read
            char[] buffer = new char[1];
            int bytesRead = await reader.ReadAsync(buffer, cancellationToken);

            if (bytesRead == 0)
            {
                // End of stream reached
                return line.Length > 0 ? line.ToString() : null;
            }

            char c = buffer[0];

            if (c == '\r' || c == '\n')
            {
                // End of line reached
                return line.ToString();
            }

            line.Append(c);
        }

        // Cancelled
        return null;
    }
}
