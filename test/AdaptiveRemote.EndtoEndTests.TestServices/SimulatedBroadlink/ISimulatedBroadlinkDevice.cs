using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;

namespace AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;

/// <summary>
/// Interface for the simulated Broadlink device, used by tests to verify packet transmission.
/// </summary>
public interface ISimulatedBroadlinkDevice : ISimulatedTiVoDevice
{
    /// <summary>
    /// Gets all packets recorded since the device started or since the last clear.
    /// </summary>
    IReadOnlyList<RecordedPacket> GetRecordedPackets();

    /// <summary>
    /// Clears all recorded packets.
    /// </summary>
    void ClearRecordedPackets();
}
