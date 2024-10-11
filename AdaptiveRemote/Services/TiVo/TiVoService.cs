using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.TiVo;

internal sealed class TiVoService : CommandServiceBase<TiVoCommand>
{
    private readonly ITiVoLocator _locator;
    private readonly ITiVoConnection.Factory _connectionFactory;
    private ITiVoConnection? _connection;

    public TiVoService(ITiVoLocator locator, ITiVoConnection.Factory connectionFactory, IRemoteDefinitionService definitionService, ILogger<TiVoService> logger)
        : base("TiVo Commands", definitionService, logger)
    {
        _locator = locator;
        _connectionFactory = connectionFactory;
    }

    public override async Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        activity.Description = Phrases.Startup_ConnectingToTiVo;

        cancellationToken.ThrowIfCancellationRequested();
        System.Net.EndPoint endpoint = await _locator.FindTiVoAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        _connection = await _connectionFactory.ConnectAsync(endpoint, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            await ((IScopedLifecycle)this).CleanUpAsync(activity, default);
            throw new TaskCanceledException();
        }

        await base.InitializeAsync(activity, cancellationToken);
    }

    public override async Task CleanUpAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        await base.CleanUpAsync(activity, cancellationToken);

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
            return _connection!.SendIRCommandAsync(command.CommandId, cancellationToken);
        };
    }
}
