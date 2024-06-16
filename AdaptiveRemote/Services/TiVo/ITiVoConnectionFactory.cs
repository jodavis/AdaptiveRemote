using System.Net;

namespace AdaptiveRemote.Services.TiVo;

internal interface ITiVoConnectionFactory
{
    Task<ITiVoConnection> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken);
}
