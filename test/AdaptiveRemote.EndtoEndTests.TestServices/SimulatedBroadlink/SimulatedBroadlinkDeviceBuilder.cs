using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;

/// <summary>
/// Builder for creating a simulated Broadlink device.
/// </summary>
public sealed class SimulatedBroadlinkDeviceBuilder : ISimulatedDeviceBuilder
{
    private readonly ILogger _logger;

    public SimulatedBroadlinkDeviceBuilder(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SimulatedBroadlinkDevice>();
    }

    /// <inheritdoc/>
    public ISimulatedDeviceBuilder WithPort(int port)
    {
        // Port configuration is not supported - always use ephemeral port
        return this;
    }

    /// <inheritdoc/>
    public ISimulatedTiVoDevice Start()
    {
        return new SimulatedBroadlinkDevice(0, _logger); // Always use port 0 for ephemeral
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to dispose in the builder
    }
}
