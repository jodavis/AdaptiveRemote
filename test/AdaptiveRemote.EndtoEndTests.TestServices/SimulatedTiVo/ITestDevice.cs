namespace AdaptiveRemote.EndtoEndTests.SimulatedTiVo;

/// <summary>
/// Represents a running simulated test device.
/// All methods are synchronous for ease of use in test scenarios.
/// </summary>
public interface ITestDevice : IDisposable
{
    /// <summary>
    /// Stops the device and releases resources. Safe to call multiple times.
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets the TCP port the device is listening on. Valid while device is running.
    /// </summary>
    int Port { get; }

    /// <summary>
    /// Returns a copy of messages recorded by the device.
    /// </summary>
    /// <returns>A read-only list of recorded messages.</returns>
    IReadOnlyList<RecordedMessage> GetRecordedMessages();

    /// <summary>
    /// Clears all recorded messages to prepare for the next test.
    /// </summary>
    void ClearRecordedMessages();
}
