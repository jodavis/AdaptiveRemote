using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Logging;

/// <summary>
/// Centralized logging messages for LayoutProcessingService.
/// All log messages MUST be defined here as [LoggerMessage] source-generated methods.
/// Event ID ranges:
///   1700-1799: LayoutProcessingService
/// </summary>
public static partial class MessageLogger
{
    [LoggerMessage(EventId = 1700, Level = LogLevel.Information, Message = "LayoutProcessingService starting")]
    public static partial void ServiceStarting(this ILogger logger);

    [LoggerMessage(EventId = 1701, Level = LogLevel.Information, Message = "LayoutProcessingService started successfully on {ListenAddress}")]
    public static partial void ServiceStarted(this ILogger logger, string listenAddress);

    [LoggerMessage(EventId = 1702, Level = LogLevel.Information, Message = "GET /health request received")]
    public static partial void HealthCheckRequested(this ILogger logger);

    [LoggerMessage(EventId = 1703, Level = LogLevel.Information, Message = "Health check successful")]
    public static partial void HealthCheckSuccessful(this ILogger logger);

    [LoggerMessage(EventId = 1704, Level = LogLevel.Error, Message = "Error processing health check request")]
    public static partial void ErrorProcessingHealthCheck(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1705,
        Level = LogLevel.Error,
        Message = "LocalStack dependency check failed at {HealthUrl}: {FailureReason}. LocalStack is required for local development. See docs/local-dev.md for setup instructions")]
    public static partial void LocalStackDependencyUnavailable(this ILogger logger, string healthUrl, string failureReason, Exception? exception);

    [LoggerMessage(EventId = 1706, Level = LogLevel.Information, Message = "SQS polling loop started; queue={QueueUrl}")]
    public static partial void SqsPollingStarted(this ILogger logger, string queueUrl);

    [LoggerMessage(EventId = 1707, Level = LogLevel.Information, Message = "SQS polling loop stopped")]
    public static partial void SqsPollingStopped(this ILogger logger);

    [LoggerMessage(EventId = 1708, Level = LogLevel.Information, Message = "SQS message received; rawLayoutId={RawLayoutId} receiptHandle={ReceiptHandle}")]
    public static partial void SqsMessageReceived(this ILogger logger, Guid rawLayoutId, string receiptHandle);

    [LoggerMessage(EventId = 1709, Level = LogLevel.Information, Message = "Layout compiled successfully; rawLayoutId={RawLayoutId}")]
    public static partial void LayoutCompiled(this ILogger logger, Guid rawLayoutId);

    [LoggerMessage(EventId = 1710, Level = LogLevel.Information, Message = "Layout validation passed; rawLayoutId={RawLayoutId}")]
    public static partial void LayoutValidationPassed(this ILogger logger, Guid rawLayoutId);

    [LoggerMessage(EventId = 1711, Level = LogLevel.Warning, Message = "Layout validation failed; rawLayoutId={RawLayoutId} issueCount={IssueCount}")]
    public static partial void LayoutValidationFailed(this ILogger logger, Guid rawLayoutId, int issueCount);

    [LoggerMessage(EventId = 1712, Level = LogLevel.Information, Message = "Compiled layout stored; rawLayoutId={RawLayoutId} compiledLayoutId={CompiledLayoutId}")]
    public static partial void CompiledLayoutStored(this ILogger logger, Guid rawLayoutId, Guid compiledLayoutId);

    [LoggerMessage(EventId = 1713, Level = LogLevel.Information, Message = "Layout-ready notification published; userId={UserId} compiledLayoutId={CompiledLayoutId}")]
    public static partial void LayoutReadyPublished(this ILogger logger, string userId, Guid compiledLayoutId);

    [LoggerMessage(EventId = 1714, Level = LogLevel.Information, Message = "SQS message processed successfully; rawLayoutId={RawLayoutId}")]
    public static partial void SqsMessageProcessedSuccessfully(this ILogger logger, Guid rawLayoutId);

    [LoggerMessage(EventId = 1715, Level = LogLevel.Error, Message = "Failed to process SQS message; rawLayoutId={RawLayoutId} receiptHandle={ReceiptHandle}")]
    public static partial void ErrorProcessingSqsMessage(this ILogger logger, Guid rawLayoutId, string receiptHandle, Exception exception);

    [LoggerMessage(EventId = 1716, Level = LogLevel.Warning, Message = "SQS message is being retried; rawLayoutId={RawLayoutId} approximateReceiveCount={ApproximateReceiveCount}")]
    public static partial void SqsMessageRetry(this ILogger logger, Guid rawLayoutId, int approximateReceiveCount);

    [LoggerMessage(EventId = 1717, Level = LogLevel.Error, Message = "SQS polling error; will retry")]
    public static partial void SqsPollingError(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1718, Level = LogLevel.Warning, Message = "Raw layout not found; rawLayoutId={RawLayoutId}")]
    public static partial void RawLayoutNotFound(this ILogger logger, Guid rawLayoutId);

    [LoggerMessage(EventId = 1719, Level = LogLevel.Information, Message = "Validation result written back to raw layout; rawLayoutId={RawLayoutId}")]
    public static partial void ValidationResultWrittenBack(this ILogger logger, Guid rawLayoutId);

    [LoggerMessage(EventId = 1720, Level = LogLevel.Warning, Message = "SQS message unrecognized and deleted; receiptHandle={ReceiptHandle}")]
    public static partial void SqsUnrecognizedMessageWarning(this ILogger logger, string receiptHandle, Exception exception);
}
