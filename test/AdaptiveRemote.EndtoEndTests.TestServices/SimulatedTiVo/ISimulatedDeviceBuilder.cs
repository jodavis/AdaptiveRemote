namespace AdaptiveRemote.EndtoEndTests.SimulatedTiVo;

/// <summary>
/// Builder pattern for configuring and starting a simulated device.
/// </summary>
public interface ISimulatedDeviceBuilder : IDisposable
{
    /// <summary>
    /// Configures the TCP port for the device. Use 0 for an ephemeral port.
    /// </summary>
    /// <param name="port">The port number to use.</param>
    /// <returns>This builder instance for fluent configuration.</returns>
    ISimulatedDeviceBuilder WithPort(int port);

    /// <summary>
    /// Starts the device synchronously and returns the running device.
    /// </summary>
    /// <returns>A running simulated TiVo device instance.</returns>
    ISimulatedTiVoDevice Start();
}
