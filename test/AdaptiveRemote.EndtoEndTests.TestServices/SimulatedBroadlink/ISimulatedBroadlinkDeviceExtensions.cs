using AdaptiveRemote.TestUtilities;

namespace AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;

/// <summary>
/// Extension methods for <see cref="ISimulatedBroadlinkDevice"/> to support test assertions.
/// </summary>
public static class ISimulatedBroadlinkDeviceExtensions
{
    /// <summary>
    /// Checks if the device has recorded at least one inbound packet with IR payload data.
    /// </summary>
    /// <param name="device">The simulated Broadlink device.</param>
    /// <returns>True if at least one inbound packet with IR data was recorded; otherwise, false.</returns>
    public static bool HasRecordedInboundPacketWithIrData(this ISimulatedBroadlinkDevice device)
    {
        IReadOnlyList<RecordedPacket> packets = device.GetRecordedPackets();
        return packets.Any(p => p.IsInbound && p.RawPayload != null && p.RawPayload.Length > 0);
    }

    /// <summary>
    /// Gets a formatted string of all recorded packets for debugging purposes.
    /// </summary>
    /// <param name="device">The simulated Broadlink device.</param>
    /// <returns>A string containing details of all recorded packets.</returns>
    public static string GetRecordedPacketsDebugString(this ISimulatedBroadlinkDevice device)
    {
        IReadOnlyList<RecordedPacket> packets = device.GetRecordedPackets();
        return string.Join(", ", packets.Select(p =>
            $"[{p.ReceivedAt:HH:mm:ss.fff}] {(p.IsInbound ? "←" : "→")} {p.DebugDescription}"));
    }

    /// <summary>
    /// Gets the first recorded packet with IR payload data, or null if none exists.
    /// </summary>
    /// <param name="device">The simulated Broadlink device.</param>
    /// <returns>The first packet with IR data, or null.</returns>
    public static RecordedPacket? GetFirstPacketWithIrData(this ISimulatedBroadlinkDevice device)
    {
        IReadOnlyList<RecordedPacket> packets = device.GetRecordedPackets();
        return packets.FirstOrDefault(p => p.IsInbound && p.RawPayload != null);
    }

    /// <summary>
    /// Checks if any recorded packet is marked as malformed.
    /// </summary>
    /// <param name="device">The simulated Broadlink device.</param>
    /// <returns>The first malformed packet, or null if none exist.</returns>
    public static RecordedPacket? GetFirstMalformedPacket(this ISimulatedBroadlinkDevice device)
    {
        IReadOnlyList<RecordedPacket> packets = device.GetRecordedPackets();
        return packets.FirstOrDefault(p => p.IsMalformed);
    }

    /// <summary>
    /// Waits until the device enters learning mode, polling with retries.
    /// </summary>
    /// <param name="device">The simulated Broadlink device.</param>
    /// <param name="timeoutInSeconds">Maximum seconds to wait.</param>
    /// <returns>True if the device entered learning mode within the timeout; otherwise, false.</returns>
    public static bool WaitForLearningMode(this ISimulatedBroadlinkDevice device, int timeoutInSeconds = 10)
        => WaitHelpers.ExecuteWithRetries(() => device.IsInLearningMode, TimeSpan.FromSeconds(timeoutInSeconds));
}
