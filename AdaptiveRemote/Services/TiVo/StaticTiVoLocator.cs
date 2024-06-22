using System.Net;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.TiVo;

internal class StaticTiVoLocator : ITiVoLocator
{
    private readonly TiVoSettings _settings;

    public StaticTiVoLocator(IOptionsSnapshot<TiVoSettings> settings)
    {
        _settings = settings.Value;
    }

    Task<EndPoint> ITiVoLocator.FindTiVoAsync(CancellationToken cancellationToken)
    {
        string ipString = _settings.IP
            ?? throw Errors.TiVo_IPAddressRequired("tivo", nameof(_settings.IP));

        EndPoint endpoint = IPEndPoint.Parse(ipString);

        return Task.FromResult(endpoint);
    }
}
