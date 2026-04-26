namespace AdaptiveRemote.Backend.RawLayoutService.Configuration;

/// <summary>
/// Configuration for AWS SQS connection used to trigger layout processing.
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
    /// AWS region (e.g. "us-east-1").
    /// </summary>
    public string Region { get; set; } = "us-east-1";
}
