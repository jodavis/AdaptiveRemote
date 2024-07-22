using System.Net;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using I8Beef.TiVo;
using I8Beef.TiVo.Commands;
using I8Beef.TiVo.Events;
using Microsoft.Extensions.Logging;
using I8BeefCommand = I8Beef.TiVo.Commands.Command;

namespace AdaptiveRemote.Services.TiVo;

internal class LibraryTiVoConnection : ITiVoConnection
{
    private Client? _client;
    private readonly string _description;
    private readonly ILogger _logger;

    private LibraryTiVoConnection(string host, int port, ILogger logger)
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
            ?? throw new ObjectDisposedException(nameof(Factory));

        await client.SendCommandAsync(new IrCommand
        {
            IrCode = commandId
        });
    }

    private void OnMessageSent(object? sender, MessageSentEventArgs e)
        => _logger.LogInformation(Message.TiVoConnection_MessageSent, e.Message);
    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        => _logger.LogInformation(Message.TiVoConnection_MessageReceived, e.Message);
    private void OnEventReceived(object? sender, ResponseEventArgs e)
        => _logger.LogInformation(Message.TiVoConnection_EventReceived, e.Response.Code, e.Response.InResponseToCode, e.Response.Value);
    private void OnError(object? sender, System.IO.ErrorEventArgs e)
        => _logger.LogInformation(Message.TiVoConnection_Error, e.GetException());

    internal class Factory : ITiVoConnection.Factory
    {
        private const int DefaultTiVoPort = 31339;

        private readonly ILogger<Client> _logger;

        public Factory(ILogger<Client> logger)
        {
            _logger = logger;
        }

        async Task<ITiVoConnection> ITiVoConnection.Factory.ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                if (GetHostAndPortFromEndpoint(endpoint, out string host, out int? port))
                {
                    return new LibraryTiVoConnection(host, port ?? DefaultTiVoPort, _logger);
                }
                else
                {
                    throw new ArgumentException($"EndPoint of type {endpoint.GetType().Name} is not supported", nameof(endpoint));
                }
            });
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
    }
}
