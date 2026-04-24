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
    private bool _disposed;
    private AdaptiveRemoteHost? _host;
    private string? _nextLogLocation;
    private string? _currentLogLocation;
    // Settings file path is determined lazily from the TestResults directory when SetLogLocation is first called.
    private string? _testSettingsPath;
    private string? _cloudCachePath;
    private string? _cloudStubFilePath;

    public SimulatedEnvironment(SimulatedTiVoDeviceBuilder tivoBuilder, SimulatedBroadlinkDeviceBuilder broadlinkBuilder, AdaptiveRemoteHost.Builder hostBuilder)
    {
        _tivo = tivoBuilder.Start();
        _broadlink = broadlinkBuilder.Start();
        _hostBuilder = hostBuilder;

        List<string> args =
        [
            // Use the simulated TiVo device
            $"--tivo:IP=127.0.0.1:{_tivo.Port}",

            // Use the simulated Broadlink device
            $"--broadlink:DiscoveryAddress=127.0.0.1",
            $"--broadlink:DiscoveryPort={_broadlink.Port}",
        ];

        hostBuilder
            .ConfigureSettings(hostSettings => hostSettings.AddCommandLineArgs(string.Join(" ", args)))
            .ConfigureTestServices(async (testEndpoint, ct) =>
            {
                // Always inject TestSpeechRecognitionEngine so tests can share the same host instance
                await testEndpoint.InjectTestServiceAsync<ISpeechRecognitionEngine, TestSpeechRecognitionEngine>(ct);
                // Always inject TestSpeechSynthesis so tests can verify spoken phrases without audio devices
                await testEndpoint.InjectTestServiceAsync<ISpeechSynthesis, TestSpeechSynthesis>(ct);
            });
    }

    /// <inheritdoc/>
    public ISimulatedTiVoDevice TiVo => _tivo;

    /// <inheritdoc/>
    public ISimulatedBroadlinkDevice Broadlink => _broadlink;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, byte[]> TestIrPayloads => _testIrPayloads;

    /// <inheritdoc/>
    public string? CloudCachePath => _cloudCachePath;

    /// <inheritdoc/>
    public string? CloudStubFilePath => _cloudStubFilePath;

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

    public void SetCloudAssetPaths(string cachePath, string stubFilePath)
    {
        _cloudCachePath = cachePath;
        _cloudStubFilePath = stubFilePath;

        _hostBuilder.ConfigureSettings(s => s.AddCommandLineArgs(
            $"--cloud:CachePath=\"{cachePath}\" --cloud:StubFilePath=\"{stubFilePath}\" --cloud:IdleCooldownSeconds=0"));
    }

    public void SetIdleCooldownSeconds(int seconds)
    {
        if (seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Idle cooldown must be non-negative.");
        }

        // Appends the arg; the configuration system uses last-wins for duplicate keys,
        // so this overrides the value set in SetCloudAssetPaths.
        _hostBuilder.ConfigureSettings(s => s.AddCommandLineArgs($"--cloud:IdleCooldownSeconds={seconds}"));
    }

    public void SetLogLocation(string logLocation)
    {
        // Ensure settings file is created (adds --programmatic arg) before adding log arg
        EnsureTestSettingsFileCreated(Path.GetDirectoryName(logLocation)!);

        _nextLogLocation = logLocation;
    }

    private void EnsureTestSettingsFileCreated(string directory)
    {
        if (_testSettingsPath is not null)
        {
            return;
        }

        _testSettingsPath = Path.Combine(directory, "ProgrammaticSettings.ini");
        WriteTestSettingsFile(_testSettingsPath, _testIrPayloads);

        _hostBuilder.ConfigureSettings(s =>
            s.AddCommandLineArgs($"--programmatic:ProgrammaticSettingsPath=\"{_testSettingsPath}\""));
    }

    private static void WriteTestSettingsFile(string path, IReadOnlyDictionary<string, byte[]> payloads)
    {
        List<string> lines = [$"[IRData]"];
        lines.AddRange(payloads.Select(kvp => $"{kvp.Key}={Convert.ToBase64String(kvp.Value)}"));
        File.WriteAllLines(path, lines);
    }
}
