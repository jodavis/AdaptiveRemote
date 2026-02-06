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
}
