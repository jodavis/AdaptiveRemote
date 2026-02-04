using AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;
using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests.Host;

/// <summary>
/// Default implementation of <see cref="ISimulatedEnvironment"/>.
/// </summary>
public sealed class SimulatedEnvironment : ISimulatedEnvironment
{
    private ISimulatedTiVoDevice? _tivo;
    private ISimulatedBroadlinkDevice? _broadlink;
    private bool _disposed;

    public SimulatedEnvironment(ILoggerFactory loggerFactory)
    {
        SimulatedTiVoDeviceBuilder tivoBuilder = new SimulatedTiVoDeviceBuilder(loggerFactory);
        SimulatedBroadlinkDeviceBuilder broadlinkBuilder = new SimulatedBroadlinkDeviceBuilder(loggerFactory);

        _tivo = tivoBuilder.Start() as ISimulatedTiVoDevice;
        _broadlink = broadlinkBuilder.Start() as ISimulatedBroadlinkDevice;
    }

    /// <inheritdoc/>
    public ISimulatedTiVoDevice? TiVo => _tivo;

    /// <inheritdoc/>
    public ISimulatedBroadlinkDevice? Broadlink => _broadlink;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _tivo?.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        try
        {
            _broadlink?.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        _tivo = null;
        _broadlink = null;
        _disposed = true;
    }
}
