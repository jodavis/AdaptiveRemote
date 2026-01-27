using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;

namespace AdaptiveRemote.EndtoEndTests.Host;

/// <summary>
/// Default implementation of <see cref="ITestEnvironment"/>.
/// </summary>
public sealed class TestEnvironment : ITestEnvironment
{
    private readonly Dictionary<string, ITestDeviceBuilder> _builders = new();
    private readonly Dictionary<string, ITestDevice> _devices = new();
    private bool _disposed;

    /// <inheritdoc/>
    public void RegisterDevice(string name, ITestDeviceBuilder builder)
    {
        if (_builders.ContainsKey(name))
        {
            throw new InvalidOperationException($"Device with name '{name}' is already registered.");
        }

        _builders[name] = builder;
    }

    /// <inheritdoc/>
    public ITestDevice StartDevice(string name)
    {
        if (!_builders.TryGetValue(name, out ITestDeviceBuilder? builder))
        {
            throw new InvalidOperationException($"No device builder registered with name '{name}'.");
        }

        if (_devices.ContainsKey(name))
        {
            throw new InvalidOperationException($"Device with name '{name}' is already started.");
        }

        ITestDevice device = builder.Start();
        _devices[name] = device;
        return device;
    }

    /// <inheritdoc/>
    public bool TryGetDevice(string name, out ITestDevice? device)
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

        foreach (ITestDevice device in _devices.Values)
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

        foreach (ITestDeviceBuilder builder in _builders.Values)
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
