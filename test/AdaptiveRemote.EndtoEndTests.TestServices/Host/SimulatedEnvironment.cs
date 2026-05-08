using AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;
using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using AdaptiveRemote.EndToEndTests.TestServices.Backend;
using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.TestUtilities;
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

        _lazyCompiledLayoutService = new(StartCompiledLayoutService);
        _lazyRawLayoutService = new(StartRawLayoutService);
        _lazyLayoutProcessingService = new(StartLayoutProcessingService);
        _lazyLocalStackFixture = new(StartLocalStack);
    }

    /// <inheritdoc/>
    public ISimulatedTiVoDevice TiVo => _tivo;

    /// <inheritdoc/>
    public ISimulatedBroadlinkDevice Broadlink => _broadlink;

    private Lazy<ServiceFixture> _lazyRawLayoutService;
    public ServiceFixture RawLayoutService => _lazyRawLayoutService.Value;

    private Lazy<ServiceFixture> _lazyCompiledLayoutService;
    public ServiceFixture CompiledLayoutService => _lazyCompiledLayoutService.Value;

    private Lazy<ServiceFixture> _lazyLayoutProcessingService;
    public ServiceFixture LayoutProcessingService => _lazyLayoutProcessingService.Value;

    private Lazy<LocalStackFixture> _lazyLocalStackFixture = new(() => new LocalStackFixture());
    public LocalStackFixture LocalStack => _lazyLocalStackFixture.Value;

    public TestJwtAuthority JwtAuthority { get; } = new();

    private ServiceFixture StartRawLayoutService()
    {
        ServiceFixture fixture = new ServiceFixture(this);
        WaitHelpers.WaitForAsyncTask(ct => fixture.StartServiceAsync("AdaptiveRemote.Backend.RawLayoutService"));
        return fixture;
    }

    private ServiceFixture StartCompiledLayoutService()
    {
        ServiceFixture fixture = new ServiceFixture(this);
        WaitHelpers.WaitForAsyncTask(ct => fixture.StartServiceAsync("AdaptiveRemote.Backend.CompiledLayoutService"));
        return fixture;
    }

    private ServiceFixture StartLayoutProcessingService()
    {
        ServiceFixture fixture = new ServiceFixture(this, new()
        {
            ["RawLayoutService__BaseUrl"] = RawLayoutService.ServiceUrl,
            ["RawLayoutService__ServiceAccountToken"] = JwtAuthority.CreateToken("service-account-layout-processor"),
            ["CompiledLayoutService__BaseUrl"] = CompiledLayoutService.ServiceUrl,

            // Enable the orchestrator for pipeline tests
            ["Orchestrator__Enabled"] = "true",
        });
        WaitHelpers.WaitForAsyncTask(ct => fixture.StartServiceAsync("AdaptiveRemote.Backend.LayoutProcessingService"));
        return fixture;
    }

    private LocalStackFixture StartLocalStack()
    {
        LocalStackFixture fixture = new LocalStackFixture();

        WaitHelpers.WaitForAsyncTask(ct => fixture.StartAsync(), timeoutInSeconds: 30);
        WaitHelpers.WaitForAsyncTask(ct => fixture.CreateSqsQueueAsync("LayoutProcessingQueue", ct), timeoutInSeconds: 10);
        WaitHelpers.WaitForAsyncTask(ct => fixture.CreateTableAsync("RawLayouts", ct), timeoutInSeconds: 10);

        return fixture;
    }

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
            if (_lazyCompiledLayoutService.IsValueCreated)
            {
                _lazyCompiledLayoutService.Value.Dispose();
            }
        }
        catch
        {
            // Ignore disposal errors
        }

        try
        {
            if (_lazyRawLayoutService.IsValueCreated)
            {
                _lazyRawLayoutService.Value.Dispose();
            }
        }
        catch
        {
            // Ignore disposal errors
        }

        try
        {
            if (_lazyLayoutProcessingService.IsValueCreated)
            {
                _lazyLayoutProcessingService.Value.Dispose();
            }
        }
        catch
        {
            // Ignore disposal errors
        }

        try
        {
            if (_lazyLocalStackFixture.IsValueCreated)
            {
                _lazyLocalStackFixture.Value.Dispose();
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
