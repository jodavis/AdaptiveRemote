namespace AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;

/// <summary>
/// Represents a recorded Broadlink packet for test verification.
/// </summary>
public sealed record RecordedPacket
{
    /// <summary>
    /// When the packet was recorded (UTC).
    /// </summary>
    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>
    /// True if the packet was received from the app (inbound to device).
    /// </summary>
    public required bool IsInbound { get; init; }

    /// <summary>
    /// The packet type/command code.
    /// </summary>
    public required short PacketType { get; init; }

    /// <summary>
    /// Raw IR payload bytes (when present).
    /// </summary>
    public byte[]? RawPayload { get; init; }

    /// <summary>
    /// True if the packet had checksum or framing errors.
    /// </summary>
    public bool IsMalformed { get; init; }

    /// <summary>
    /// Debug description of the packet.
    /// </summary>
    public string DebugDescription { get; init; } = string.Empty;
}
