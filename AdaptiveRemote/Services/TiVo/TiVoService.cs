using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.TiVo;

internal class TiVoService : IScopedLifecycle
{
    private readonly ITiVoLocator _locator;
    private readonly ITiVoConnectionFactory _connectionFactory;
    private readonly IEnumerable<TiVoCommand> _commands;
    private ITiVoConnection? _connection;

    public TiVoService(ITiVoLocator locator, ITiVoConnectionFactory connectionFactory, IRemoteDefinitionService definitionService)
    {
        _locator = locator;
        _connectionFactory = connectionFactory;
        _commands = definitionService.GetCommands<TiVoCommand>().ToList();

        // TODO: List commands and initialize ExecuteAsync/Enabled?
    }

    string IScopedLifecycle.Name => "TiVo Control System";

    async Task IScopedLifecycle.InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        System.Net.EndPoint endpoint = await _locator.FindTiVoAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        _connection = await _connectionFactory.ConnectAsync(endpoint, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            await ((IScopedLifecycle)this).CleanUpAsync(default);
            throw new TaskCanceledException();
        }

        foreach (TiVoCommand command in _commands)
        {
            command.ExecuteAsync = cancellationToken =>
            {
                // TODO: Logging/error handling/IsActive (using the shared wrapper)
                return _connection.SendIRCommandAsync(command.CommandId, cancellationToken);
            };
            command.IsEnabled = true;
        }
    }

    async Task IScopedLifecycle.CleanUpAsync(CancellationToken cancellationToken)
    {
        // TODO: Clean up ExecuteAsync from all commands
        ITiVoConnection? connectionToDispose = Interlocked.Exchange(ref _connection, null);
        if (connectionToDispose is not null)
        {
            await connectionToDispose.DisposeAsync(cancellationToken);
        }
    }
}
