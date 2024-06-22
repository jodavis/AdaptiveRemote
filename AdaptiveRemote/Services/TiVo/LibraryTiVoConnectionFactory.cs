using System.Net;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using I8Beef.TiVo;
using I8Beef.TiVo.Commands;
using I8Beef.TiVo.Events;
using Microsoft.Extensions.Logging;
using I8BeefCommand = I8Beef.TiVo.Commands.Command;

namespace AdaptiveRemote.Services.TiVo;

internal class LibraryTiVoConnectionFactory : ITiVoConnectionFactory
{
    private const int DefaultTiVoPort = 31339;

    private readonly ILogger<Client> _logger;

    public LibraryTiVoConnectionFactory(ILogger<Client> logger)
    {
        _logger = logger;
    }

    Task<ITiVoConnection> ITiVoConnectionFactory.ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken)
    {
        if (GetHostAndPortFromEndpoint(endpoint, out string host, out int? port))
        {
            return Task.FromResult<ITiVoConnection>(new Connection(host, port ?? DefaultTiVoPort, _logger));
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
        private readonly string _description;
        private readonly ILogger _logger;

        public Connection(string host, int port, ILogger logger)
        {
            _client = new Client(host, port);
            _description = $"{host}:{port}";
            _logger = logger;

            _client.Error += OnError;
            _client.EventReceived += OnEventReceived;
            _client.MessageReceived += OnMessageReceived;
            _client.MessageSent += OnMessageSent;

            _client.Connect();
            _logger.LogInformation(Message.TiVoConnection_Connected, _description);
        }

        Task ITiVoConnection.DisposeAsync(CancellationToken cancellationToken)
        {
            Client? client = Interlocked.Exchange(ref _client, null);

            if (client is not null)
            {
                _logger.LogInformation(Message.TiVoConnection_Disconnecting, _description);

                client.Close();
                client.Dispose();

                client.Error -= OnError;
                client.EventReceived -= OnEventReceived;
                client.MessageReceived -= OnMessageReceived;
                client.MessageSent -= OnMessageSent;
            }

            return Task.CompletedTask;
        }

        async Task ITiVoConnection.SendIRCommandAsync(string commandId, CancellationToken cancellationToken)
        {
            Client client = _client
                ?? throw new ObjectDisposedException(nameof(LibraryTiVoConnectionFactory));

            I8BeefCommand command = CommandFactory.GetCommand("IRCODE " + commandId) ?? throw Errors.TiVo_CannotInterpretCommand(commandId, nameof(commandId));
            await client.SendCommandAsync(command);
        }

        private void OnMessageSent(object? sender, MessageSentEventArgs e)
            => _logger.LogInformation(Message.TiVoConnection_MessageSent, e.Message);
        private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
            => _logger.LogInformation(Message.TiVoConnection_MessageReceived, e.Message);
        private void OnEventReceived(object? sender, ResponseEventArgs e)
            => _logger.LogInformation(Message.TiVoConnection_EventReceived, e.Response);
        private void OnError(object? sender, System.IO.ErrorEventArgs e)
            => _logger.LogInformation(Message.TiVoConnection_Error, e.GetException());
    }
}
