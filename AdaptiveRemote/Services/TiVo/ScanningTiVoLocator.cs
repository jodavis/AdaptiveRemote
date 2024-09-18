using System.Net;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.TiVo;

internal class ScanningTiVoLocator : ITiVoLocator
{
    private readonly TiVoSettings _settings;

    public ScanningTiVoLocator(IOptionsSnapshot<TiVoSettings> settings)
    {
        _settings = settings.Value;
    }

    Task<EndPoint> ITiVoLocator.FindTiVoAsync(CancellationToken cancellationToken)
    {
        string ipString = _settings.IP
            ?? throw Errors.TiVo_SettingRequiredToConnect(nameof(_settings.IP));

        EndPoint endpoint = IPEndPoint.Parse(ipString);

        return Task.FromResult(endpoint);
    }
}
