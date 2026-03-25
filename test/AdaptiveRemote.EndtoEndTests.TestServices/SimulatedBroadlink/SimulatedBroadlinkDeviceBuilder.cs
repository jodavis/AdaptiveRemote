using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;

/// <summary>
/// Builder for creating a simulated Broadlink device.
/// </summary>
public sealed class SimulatedBroadlinkDeviceBuilder : IDisposable
{
    private readonly ILogger _logger;

    public SimulatedBroadlinkDeviceBuilder(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SimulatedBroadlinkDevice>();
    }

    /// <summary>
    /// Starts the device synchronously and returns the running device.
    /// </summary>
    /// <returns>A running simulated Broadlink device instance.</returns>
    public ISimulatedBroadlinkDevice Start()
    {
        return new SimulatedBroadlinkDevice(0, _logger); // Always use port 0 for ephemeral
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to dispose in the builder
    }
}
