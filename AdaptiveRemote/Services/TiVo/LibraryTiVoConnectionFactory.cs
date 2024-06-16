using System.Net;
using I8Beef.TiVo;
using I8Beef.TiVo.Commands;
using I8Beef.TiVo.Events;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.TiVo;

internal class LibraryTiVoConnectionFactory : ITiVoConnectionFactory
{
    private readonly ILogger<Client> _logger;

    public LibraryTiVoConnectionFactory(ILogger<Client> logger)
    {
        _logger = logger;
    }

    Task<ITiVoConnection> ITiVoConnectionFactory.ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken)
    {
        return Task.FromResult<ITiVoConnection>(new Connection(new(endpoint.ToString()), _logger));
    }

    private class Connection : ITiVoConnection
    {
        private Client? _client;
        private readonly ILogger _logger;

        public Connection(Client client, ILogger logger)
        {
            _client = client;
            _logger = logger;

            _client.Error += OnError;
            _client.EventReceived += OnEventReceived;
            _client.MessageReceived += OnMessageReceived;
            _client.MessageSent += OnMessageSent;

            _client.Connect();
        }

        Task ITiVoConnection.DisposeAsync(CancellationToken cancellationToken)
        {
            Client? client = Interlocked.Exchange(ref _client, null);

            if (client is not null)
            {
                client.Close();
                client.Dispose();

                client.Error -= OnError;
                client.EventReceived -= OnEventReceived;
                client.MessageReceived -= OnMessageReceived;
                client.MessageSent -= OnMessageSent;
            }

            return Task.CompletedTask;
        }

        async Task ITiVoConnection.SendAsync(string commandId, CancellationToken cancellationToken)
        {
            Client client = _client
                ?? throw new ObjectDisposedException(nameof(LibraryTiVoConnectionFactory));

            Command command = CommandFactory.GetCommand(commandId) ?? throw new ArgumentException($"Unable to interpret '{commandId}' as a TiVo command", nameof(commandId));
            await client.SendCommandAsync(command);
        }

        private void OnMessageSent(object? sender, MessageSentEventArgs e)
            => _logger.LogInformation($"Message sent: {e.Message}");
        private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
            => _logger.LogInformation($"Message received: {e.Message}");
        private void OnEventReceived(object? sender, ResponseEventArgs e)
            => _logger.LogInformation($"Event received: {e.Response}");
        private void OnError(object? sender, System.IO.ErrorEventArgs e)
            => _logger.LogError($"Error: {e.GetException()}");
    }
}
