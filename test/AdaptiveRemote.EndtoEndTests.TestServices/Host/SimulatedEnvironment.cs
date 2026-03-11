using AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;
using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdaptiveRemote.EndtoEndTests.Host;

/// <summary>
/// Default implementation of <see cref="ISimulatedEnvironment"/>.
/// </summary>
public sealed class SimulatedEnvironment : ISimulatedEnvironment
{
    private readonly ISimulatedTiVoDevice _tivo;
    private readonly ISimulatedBroadlinkDevice _broadlink;
    private readonly AdaptiveRemoteHost.Builder _hostBuilder;
    private bool _disposed;
    private AdaptiveRemoteHost? _host;
    private string? _nextLogLocation;
    private string? _currentLogLocation;

    public SimulatedEnvironment(SimulatedTiVoDeviceBuilder tivoBuilder, SimulatedBroadlinkDeviceBuilder broadlinkBuilder, AdaptiveRemoteHost.Builder hostBuilder)
    {
        _tivo = tivoBuilder.Start();
        _broadlink = (ISimulatedBroadlinkDevice)broadlinkBuilder.Start();
        _hostBuilder = hostBuilder;

        List<string> args =
        [
            // Use the simulated TiVo device
            $"--tivo:IP=127.0.0.1:{_tivo.Port}",

            // Use the simulated Broadlink device
            $"--broadlink:DiscoveryAddress=127.0.0.1",
            $"--broadlink:DiscoveryPort={_broadlink.Port}",
        ];

        hostBuilder.ConfigureSettings(hostSettings => hostSettings.AddCommandLineArgs(string.Join(" ", args)));
    }

    /// <inheritdoc/>
    public ISimulatedTiVoDevice TiVo => _tivo;

    /// <inheritdoc/>
    public ISimulatedBroadlinkDevice Broadlink => _broadlink;

    public AdaptiveRemoteHost Host
    {
        get
        {
            Assert.IsNotNull(_host, "Host has not been started. Call StartHost() or EnsureHostStarted() first.");
            Assert.IsTrue(_host.IsRunning, "Host was stopped. Restart it by calling StartHost() or EnsureHostStarted() first.");
            return _host;
        }
    }

    public string? HostLogs => _currentLogLocation;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            StopHostIfRunning();
        }
        catch
        {
            // Ignore disposal errors
        }

        try
        {
            _tivo.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        try
        {
            _broadlink.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        _disposed = true;
    }

    public void EnsureHostStarted()
    {
        if (_host is null || !_host.IsRunning)
        {
            _currentLogLocation = _nextLogLocation;
            _host?.Dispose();
            _host = null;
            _host = _hostBuilder.Start(_currentLogLocation is not null
                ? settings => settings.AddCommandLineArgs($"--log:FilePath=\"{_currentLogLocation}\"")
                : null);
        }
    }

    void ISimulatedEnvironment.StartHost()
    {
        Assert.IsFalse(_host?.IsRunning == true, "Host is already running. Stop it first before starting a new instance.");
        EnsureHostStarted();
    }

    public void StopHostIfRunning()
    {
        if (Interlocked.Exchange(ref _host, null) is AdaptiveRemoteHost runningHost)
        {
            runningHost.Stop();
            runningHost.Dispose();
        }
    }

    public void SetLogLocation(string logLocation)
    {
        _nextLogLocation = logLocation;
    }
}
