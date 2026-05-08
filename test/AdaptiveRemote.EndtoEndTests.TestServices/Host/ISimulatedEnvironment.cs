using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;
using AdaptiveRemote.EndToEndTests.TestServices.Backend;

namespace AdaptiveRemote.EndtoEndTests.SimulatedTiVo;

/// <summary>
/// Manages simulated devices for a test run.
/// </summary>
public interface ISimulatedEnvironment : IDisposable
{
    /// <summary>
    /// Gets the simulated TiVo device, if started.
    /// </summary>
    ISimulatedTiVoDevice TiVo { get; }

    /// <summary>
    /// Gets the simulated Broadlink device, if started.
    /// </summary>
    ISimulatedBroadlinkDevice Broadlink { get; }

    TestJwtAuthority JwtAuthority { get; }

    ServiceFixture RawLayoutService { get; }

    ServiceFixture CompiledLayoutService { get; }

    ServiceFixture LayoutProcessingService { get; }

    LocalStackFixture LocalStack { get; }

    void EnsureHostStarted();

    void StartHost();

    void StopHostIfRunning();

    AdaptiveRemoteHost Host { get; }

    string? HostLogs { get; }

    /// <summary>
    /// Gets the test-time IR payloads that are programmed into the settings file.
    /// Keys are command names (e.g. "Power"); values are the raw IR bytes.
    /// Commands not present in this dictionary are not programmed and should be disabled.
    /// </summary>
    IReadOnlyDictionary<string, byte[]> TestIrPayloads { get; }
}
