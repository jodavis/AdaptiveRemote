using System.Net;
using System.Reflection.Metadata.Ecma335;
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
        if (GetHostAndPortFromEndpoint(endpoint, out string host, out int? port))
        {
            return Task.FromResult<ITiVoConnection>(new Connection(new(host, port ?? 31339), _logger));
        }
        else
        {
            return Task.FromException<ITiVoConnection>(new ArgumentException($"EndPoint of type {endpoint.GetType().Name} is not supported", nameof(endpoint)));
        }
    }

    private static bool GetHostAndPortFromEndpoint(EndPoint endpoint, out string host, out int? port)
    {
        switch (endpoint)
        {
            case IPEndPoint ipEndPoint:
                host = ipEndPoint.Address.ToString();
                port = ipEndPoint.Port;
                if (port == 0)
                {
                    port = null;
                }
                return true;

            default:
                host = string.Empty;
                port = null;
                return false;
        }
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
            _logger.LogInformation($"Connected to TiVo {_client}");
        }

        Task ITiVoConnection.DisposeAsync(CancellationToken cancellationToken)
        {
            Client? client = Interlocked.Exchange(ref _client, null);

            if (client is not null)
            {
                _logger.LogInformation($"Disconecting from TiVo {client}");

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

            Command command = CommandFactory.GetCommand("IRCODE " + commandId) ?? throw new ArgumentException($"Unable to interpret '{commandId}' as a TiVo command", nameof(commandId));
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
