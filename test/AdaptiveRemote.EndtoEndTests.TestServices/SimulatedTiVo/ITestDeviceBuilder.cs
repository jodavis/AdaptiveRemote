namespace AdaptiveRemote.EndtoEndTests.SimulatedTiVo;

/// <summary>
/// Builder pattern for configuring and starting a simulated test device.
/// </summary>
public interface ITestDeviceBuilder : IDisposable
{
    /// <summary>
    /// Configures the TCP port for the device. Use 0 for an ephemeral port.
    /// </summary>
    /// <param name="port">The port number to use.</param>
    /// <returns>This builder instance for fluent configuration.</returns>
    ITestDeviceBuilder WithPort(int port);

    /// <summary>
    /// Starts the device synchronously and returns the running device.
    /// </summary>
    /// <returns>A running test device instance.</returns>
    ITestDevice Start();
}
