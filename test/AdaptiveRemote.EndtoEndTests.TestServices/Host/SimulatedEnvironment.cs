using AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;
using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;

namespace AdaptiveRemote.EndtoEndTests.Host;

/// <summary>
/// Default implementation of <see cref="ISimulatedEnvironment"/>.
/// </summary>
public sealed class SimulatedEnvironment : ISimulatedEnvironment
{
    private const string TiVoDeviceName = "TiVo";
    private const string BroadlinkDeviceName = "Broadlink";

    private readonly Dictionary<string, ISimulatedDeviceBuilder> _builders = new();
    private readonly Dictionary<string, ISimulatedDevice> _devices = new();
    private bool _disposed;

    /// <inheritdoc/>
    public ISimulatedDevice? TiVo => _devices.TryGetValue(TiVoDeviceName, out ISimulatedDevice? device) ? device : null;

    /// <inheritdoc/>
    public ISimulatedBroadlinkDevice? Broadlink => _devices.TryGetValue(BroadlinkDeviceName, out ISimulatedDevice? device) ? device as ISimulatedBroadlinkDevice : null;

    /// <inheritdoc/>
    public void RegisterDevice(string name, ISimulatedDeviceBuilder builder)
    {
        if (_builders.ContainsKey(name))
        {
            throw new InvalidOperationException($"Device with name '{name}' is already registered.");
        }

        _builders[name] = builder;
    }

    /// <inheritdoc/>
    public ISimulatedDevice StartDevice(string name)
    {
        if (!_builders.TryGetValue(name, out ISimulatedDeviceBuilder? builder))
        {
            throw new InvalidOperationException($"No device builder registered with name '{name}'.");
        }

        if (_devices.ContainsKey(name))
        {
            throw new InvalidOperationException($"Device with name '{name}' is already started.");
        }

        ISimulatedDevice device = builder.Start();
        _devices[name] = device;
        return device;
    }

    /// <inheritdoc/>
    public bool TryGetDevice(string name, out ISimulatedDevice? device)
    {
        return _devices.TryGetValue(name, out device);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (ISimulatedDevice device in _devices.Values)
        {
            try
            {
                device.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        _devices.Clear();

        foreach (ISimulatedDeviceBuilder builder in _builders.Values)
        {
            try
            {
                builder.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        _builders.Clear();
        _disposed = true;
    }
}
