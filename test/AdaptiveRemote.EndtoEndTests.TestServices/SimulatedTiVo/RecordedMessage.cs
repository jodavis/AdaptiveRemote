namespace AdaptiveRemote.EndtoEndTests.SimulatedTiVo;

/// <summary>
/// Represents a message recorded by a simulated test device.
/// </summary>
public sealed record RecordedMessage
{
    /// <summary>
    /// Gets the timestamp when the message was recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the raw ASCII payload of the message.
    /// </summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this message was received from the application (true) or sent to it (false).
    /// </summary>
    public bool Incoming { get; init; }
}
