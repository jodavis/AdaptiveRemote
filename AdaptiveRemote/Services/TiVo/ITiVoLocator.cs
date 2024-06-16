using System.Net;

namespace AdaptiveRemote.Services.TiVo;

internal interface ITiVoLocator
{
    Task<EndPoint> FindTiVoAsync(CancellationToken cancellationToken);
}
