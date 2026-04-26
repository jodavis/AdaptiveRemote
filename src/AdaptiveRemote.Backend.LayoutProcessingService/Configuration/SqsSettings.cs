namespace AdaptiveRemote.Backend.LayoutProcessingService.Configuration;

/// <summary>
/// Configuration for AWS SQS connection.
/// Maps to the "Sqs" section in appsettings.json.
/// </summary>
public class SqsSettings
{
    /// <summary>
    /// The SQS service URL. For LocalStack: http://localhost:4566
    /// For AWS: leave empty to use default AWS endpoint.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// The URL of the layout processing queue.
    /// </summary>
    public string QueueUrl { get; set; } = string.Empty;

    /// <summary>
    /// The URL of the dead letter queue. Used for DLQ arrival logging.
    /// </summary>
    public string DlqUrl { get; set; } = string.Empty;

    /// <summary>
    /// AWS region (e.g. "us-east-1").
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Maximum number of messages to retrieve per SQS poll. Must be between 1 and 10.
    /// </summary>
    public int MaxNumberOfMessages { get; set; } = 10;

    /// <summary>
    /// Visibility timeout in seconds. How long a dequeued message is hidden from other consumers.
    /// </summary>
    public int VisibilityTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Long-poll wait time in seconds. 0 disables long polling.
    /// </summary>
    public int WaitTimeSeconds { get; set; } = 5;

    /// <summary>
    /// Delay between SQS polls when no messages are returned, in milliseconds.
    /// </summary>
    public int EmptyQueueDelayMs { get; set; } = 2000;
}
