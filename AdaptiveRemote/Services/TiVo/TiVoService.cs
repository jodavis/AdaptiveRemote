using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.TiVo;

internal class TiVoService : IScopedLifecycle, ITiVoService
{
    private readonly ITiVoLocator _locator;
    private readonly ITiVoConnectionFactory _connectionFactory;
    private ITiVoConnection? _connection;

    public TiVoService(ITiVoLocator locator, ITiVoConnectionFactory connectionFactory)
    {
        _locator = locator;
        _connectionFactory = connectionFactory;
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
    }

    async Task IScopedLifecycle.CleanUpAsync(CancellationToken cancellationToken)
    {
        ITiVoConnection? connectionToDispose = Interlocked.Exchange(ref _connection, null);
        if (connectionToDispose is not null)
        {
            await connectionToDispose.DisposeAsync(cancellationToken);
        }
    }

    async Task ITiVoService.SendAsync(string commandCode, CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            throw Errors.TiVo_NotInitialized(commandCode);
        }

        await _connection.SendAsync(commandCode, cancellationToken);
    }
}
