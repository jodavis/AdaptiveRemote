using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;

/// <summary>
/// Builder for creating a simulated Broadlink device.
/// </summary>
public sealed class SimulatedBroadlinkDeviceBuilder : ISimulatedDeviceBuilder
{
    private readonly ILogger _logger;
    private int _port;

    public SimulatedBroadlinkDeviceBuilder(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public ISimulatedDeviceBuilder WithPort(int port)
    {
        _port = port;
        return this;
    }

    /// <inheritdoc/>
    public ISimulatedDevice Start()
    {
        return new SimulatedBroadlinkDevice(_port, _logger);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to dispose in the builder
    }
}
