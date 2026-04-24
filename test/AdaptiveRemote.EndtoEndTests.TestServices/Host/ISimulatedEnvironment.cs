using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;

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

    /// <summary>
    /// Gets the cloud asset cache directory path configured for the current test run, or null if not configured.
    /// </summary>
    string? CloudCachePath { get; }

    /// <summary>
    /// Gets the stub layout file path configured for the current test run, or null if not configured.
    /// </summary>
    string? CloudStubFilePath { get; }

    /// <summary>
    /// Overrides the idle cooldown for the next host start.
    /// Appends a command-line arg that supersedes the default configured in SetCloudAssetPaths.
    /// </summary>
    void SetIdleCooldownSeconds(int seconds);
}
