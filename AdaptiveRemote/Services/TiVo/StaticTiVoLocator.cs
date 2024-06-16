using System.Configuration;
using System.Net;
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
        string ipString = _settings.StaticIP
            ?? throw new ConfigurationErrorsException("The StaticIP setting is required to connect to the TiVo");

        EndPoint endpoint = IPEndPoint.Parse(ipString);

        return Task.FromResult(endpoint);
    }
}
