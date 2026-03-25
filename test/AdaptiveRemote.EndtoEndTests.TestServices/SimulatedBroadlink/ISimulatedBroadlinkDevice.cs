namespace AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;

/// <summary>
/// Interface for the simulated Broadlink device, used by tests to verify packet transmission.
/// </summary>
public interface ISimulatedBroadlinkDevice : IDisposable
{
    /// <summary>
    /// Gets the UDP port the device is listening on. Valid while device is running.
    /// </summary>
    int Port { get; }

    /// <summary>
    /// Stops the device and releases resources. Safe to call multiple times.
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets all packets recorded since the device started or since the last clear.
    /// </summary>
    IReadOnlyList<RecordedPacket> GetRecordedPackets();

    /// <summary>
    /// Clears all recorded packets.
    /// </summary>
    void ClearRecordedPackets();

    /// <summary>
    /// Gets whether the device has received an enter-learning-mode command and is now
    /// waiting for an IR signal to be captured.
    /// </summary>
    bool IsInLearningMode { get; }

    /// <summary>
    /// Provides IR data that will be returned on the next <c>CheckLearnedData</c> poll.
    /// Call this to simulate a user pressing a button on their physical remote.
    /// </summary>
    /// <param name="data">The raw IR signal bytes to return to the application.</param>
    void ProvideLearnedData(byte[] data);

    /// <summary>
    /// Instructs the device to respond with the specified error code on the next
    /// <c>CheckLearnedData</c> poll, simulating a device error or timeout.
    /// </summary>
    /// <param name="errorCode">The Broadlink error code to return (e.g., <c>-3</c> for device offline).</param>
    void SimulateNextCheckError(short errorCode);
}
