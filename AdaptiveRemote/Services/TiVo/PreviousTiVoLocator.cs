using System.Net;

namespace AdaptiveRemote.Services.TiVo;

internal class PreviousTiVoLocator : ITiVoLocator
{
    private readonly ITiVoLocator _inner;

    public PreviousTiVoLocator(ITiVoLocator inner)
    {
        _inner = inner;
    }

    Task<EndPoint> ITiVoLocator.FindTiVoAsync(CancellationToken cancellationToken)
    {
        return _inner.FindTiVoAsync(cancellationToken);
    }
}
