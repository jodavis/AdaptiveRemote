using AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;
using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using AdaptiveRemote.Services.Conversation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdaptiveRemote.EndtoEndTests.Host;

/// <summary>
/// Default implementation of <see cref="ISimulatedEnvironment"/>.
/// </summary>
public sealed class SimulatedEnvironment : ISimulatedEnvironment
{
    /// <summary>
    /// Test IR payloads programmed into the test-time settings file.
    /// Commands present here will be enabled; commands absent will be disabled.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, byte[]> _testIrPayloads = new Dictionary<string, byte[]>
    {
        // Power: test payload bytes [0x01, 0x02, 0x03, 0x04]
        ["Power"] = [0x01, 0x02, 0x03, 0x04],
        // VolumeUp: test payload bytes [0x05, 0x06, 0x07, 0x08]
        ["VolumeUp"] = [0x05, 0x06, 0x07, 0x08],
    };

    private readonly ISimulatedTiVoDevice _tivo;
    private readonly ISimulatedBroadlinkDevice _broadlink;
    private readonly AdaptiveRemoteHost.Builder _hostBuilder;
    // _testSettingsPath is stored as a field to support cleanup during Dispose().
    private readonly string _testSettingsPath;
    private bool _disposed;
    private AdaptiveRemoteHost? _host;
    private string? _nextLogLocation;
    private string? _currentLogLocation;

    public SimulatedEnvironment(SimulatedTiVoDeviceBuilder tivoBuilder, SimulatedBroadlinkDeviceBuilder broadlinkBuilder, AdaptiveRemoteHost.Builder hostBuilder)
    {
        _tivo = tivoBuilder.Start();
        _broadlink = (ISimulatedBroadlinkDevice)broadlinkBuilder.Start();
        _hostBuilder = hostBuilder;

        // Create a test-time settings file with a subset of programmed IR commands.
        _testSettingsPath = Path.Combine(Path.GetTempPath(), $"AdaptiveRemote_TestSettings_{Guid.NewGuid():N}.ini");
        WriteTestSettingsFile(_testSettingsPath, _testIrPayloads);

        List<string> args =
        [
            // Use the simulated TiVo device
            $"--tivo:IP=127.0.0.1:{_tivo.Port}",

            // Use the simulated Broadlink device
            $"--broadlink:DiscoveryAddress=127.0.0.1",
            $"--broadlink:DiscoveryPort={_broadlink.Port}",

            // Use the test-time programmatic settings file
            $"--programmatic:ProgrammaticSettingsPath={_testSettingsPath}",
        ];

        hostBuilder
            .ConfigureSettings(hostSettings => hostSettings.AddCommandLineArgs(string.Join(" ", args)))
            .ConfigureTestServices(async (testEndpoint, ct) =>
            {
                // Always inject TestSpeechRecognitionEngine so tests can share the same host instance
                await testEndpoint.InjectTestServiceAsync<ISpeechRecognitionEngine, TestSpeechRecognitionEngine>(ct);
            });
    }

    /// <inheritdoc/>
    public ISimulatedTiVoDevice TiVo => _tivo;

    /// <inheritdoc/>
    public ISimulatedBroadlinkDevice Broadlink => _broadlink;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, byte[]> TestIrPayloads => _testIrPayloads;

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

        try
        {
            if (File.Exists(_testSettingsPath))
            {
                File.Delete(_testSettingsPath);
            }
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
            _host = _hostBuilder.Start();
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

    public void SetLogLocation(string logLocation) => _hostBuilder.ConfigureSettings(settings =>
    {
        _nextLogLocation = logLocation;
        return settings.AddCommandLineArgs($"--log:FilePath=\"{logLocation}\"");
    });

    private static void WriteTestSettingsFile(string path, IReadOnlyDictionary<string, byte[]> payloads)
    {
        IEnumerable<string> lines = payloads.Select(kvp =>
            $"IRData:{kvp.Key}={Convert.ToBase64String(kvp.Value)}");
        File.WriteAllLines(path, lines);
    }
}
