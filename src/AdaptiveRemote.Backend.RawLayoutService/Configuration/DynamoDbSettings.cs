namespace AdaptiveRemote.Backend.RawLayoutService.Configuration;

/// <summary>
/// Configuration for AWS DynamoDB connection.
/// Maps to the "DynamoDB" section in appsettings.json.
/// </summary>
public class DynamoDbSettings
{
    /// <summary>
    /// The DynamoDB service URL. For LocalStack: http://localhost:4566
    /// For AWS: leave empty to use default AWS endpoint.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// The name of the DynamoDB table for raw layouts.
    /// </summary>
    public string TableName { get; set; } = "RawLayouts";

    /// <summary>
    /// AWS region (e.g. "us-east-1"). Required for both LocalStack and AWS.
    /// </summary>
    public string Region { get; set; } = "us-east-1";
}
