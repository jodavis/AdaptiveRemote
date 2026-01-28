namespace AdaptiveRemote.EndtoEndTests.SimulatedTiVo;

/// <summary>
/// Manages simulated devices for a test run.
/// </summary>
public interface ISimulatedEnvironment : IDisposable
{
    /// <summary>
    /// Registers a device builder with the given name.
    /// </summary>
    /// <param name="name">The unique name for the device.</param>
    /// <param name="builder">The device builder to register.</param>
    void RegisterDevice(string name, ISimulatedDeviceBuilder builder);

    /// <summary>
    /// Starts a registered device and returns the running device instance.
    /// </summary>
    /// <param name="name">The name of the device to start.</param>
    /// <returns>The running simulated device.</returns>
    ISimulatedDevice StartDevice(string name);

    /// <summary>
    /// Attempts to retrieve a running device by name.
    /// </summary>
    /// <param name="name">The name of the device to retrieve.</param>
    /// <param name="device">The running device, if found.</param>
    /// <returns>True if the device was found; otherwise, false.</returns>
    bool TryGetDevice(string name, out ISimulatedDevice? device);
}
