using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.TiVo;

internal sealed class TiVoService : CommandServiceBase<TiVoCommand>
{
    private readonly ITiVoLocator _locator;
    private readonly ITiVoConnection.Factory _connectionFactory;
    private ITiVoConnection? _connection;

    public TiVoService(ITiVoLocator locator, ITiVoConnection.Factory connectionFactory, IRemoteDefinitionService definitionService, ILogger<TiVoService> logger)
        : base("TiVo Control System", definitionService)
    {
        _locator = locator;
        _connectionFactory = connectionFactory;
        Logger = logger;
    }

    protected override ILogger Logger { get; }

    public override async Task InitializeAsync(CancellationToken cancellationToken)
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

        await base.InitializeAsync(cancellationToken);
    }

    public override async Task CleanUpAsync(CancellationToken cancellationToken)
    {
        await base.CleanUpAsync(cancellationToken);

        ITiVoConnection? connectionToDispose = Interlocked.Exchange(ref _connection, null);
        if (connectionToDispose is not null)
        {
            await connectionToDispose.DisposeAsync(cancellationToken);
        }
    }

    protected override Command.ExecuteDelegate CreateHandler(TiVoCommand command)
    {
        return cancellationToken =>
        {
            // TODO: Logging/error handling/IsActive (using the shared wrapper)
            return _connection!.SendIRCommandAsync(command.CommandId, cancellationToken);
        };
    }
}
