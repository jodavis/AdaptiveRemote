using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Logging;

/// <summary>
/// Centralized logging messages for LayoutProcessingService.
/// All log messages MUST be defined here as [LoggerMessage] source-generated methods.
/// Event ID ranges:
///   1300-1399: LayoutProcessingService
/// </summary>
public static partial class MessageLogger
{
    [LoggerMessage(EventId = 1300, Level = LogLevel.Information, Message = "LayoutProcessingService starting")]
    public static partial void ServiceStarting(this ILogger logger);

    [LoggerMessage(EventId = 1301, Level = LogLevel.Information, Message = "LayoutProcessingService started successfully on {ListenAddress}")]
    public static partial void ServiceStarted(this ILogger logger, string listenAddress);

    [LoggerMessage(EventId = 1302, Level = LogLevel.Information, Message = "GET /health request received")]
    public static partial void HealthCheckRequested(this ILogger logger);

    [LoggerMessage(EventId = 1303, Level = LogLevel.Information, Message = "Health check successful")]
    public static partial void HealthCheckSuccessful(this ILogger logger);

    [LoggerMessage(EventId = 1304, Level = LogLevel.Error, Message = "Error processing health check request")]
    public static partial void ErrorProcessingHealthCheck(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1305,
        Level = LogLevel.Error,
        Message = "LocalStack dependency check failed at {HealthUrl}: {FailureReason}. LocalStack is required for local development. See docs/local-dev.md for setup instructions")]
    public static partial void LocalStackDependencyUnavailable(this ILogger logger, string healthUrl, string failureReason, Exception? exception);

    [LoggerMessage(EventId = 1306, Level = LogLevel.Information, Message = "SQS polling loop started; queue={QueueUrl}")]
    public static partial void SqsPollingStarted(this ILogger logger, string queueUrl);

    [LoggerMessage(EventId = 1307, Level = LogLevel.Information, Message = "SQS polling loop stopped")]
    public static partial void SqsPollingStoped(this ILogger logger);

    [LoggerMessage(EventId = 1308, Level = LogLevel.Information, Message = "SQS message received; rawLayoutId={RawLayoutId} receiptHandle={ReceiptHandle}")]
    public static partial void SqsMessageReceived(this ILogger logger, Guid rawLayoutId, string receiptHandle);

    [LoggerMessage(EventId = 1309, Level = LogLevel.Information, Message = "Layout compiled successfully; rawLayoutId={RawLayoutId}")]
    public static partial void LayoutCompiled(this ILogger logger, Guid rawLayoutId);

    [LoggerMessage(EventId = 1310, Level = LogLevel.Information, Message = "Layout validation passed; rawLayoutId={RawLayoutId}")]
    public static partial void LayoutValidationPassed(this ILogger logger, Guid rawLayoutId);

    [LoggerMessage(EventId = 1311, Level = LogLevel.Warning, Message = "Layout validation failed; rawLayoutId={RawLayoutId} issueCount={IssueCount}")]
    public static partial void LayoutValidationFailed(this ILogger logger, Guid rawLayoutId, int issueCount);

    [LoggerMessage(EventId = 1312, Level = LogLevel.Information, Message = "Compiled layout stored; rawLayoutId={RawLayoutId} compiledLayoutId={CompiledLayoutId}")]
    public static partial void CompiledLayoutStored(this ILogger logger, Guid rawLayoutId, Guid compiledLayoutId);

    [LoggerMessage(EventId = 1313, Level = LogLevel.Information, Message = "Layout-ready notification published; userId={UserId} compiledLayoutId={CompiledLayoutId}")]
    public static partial void LayoutReadyPublished(this ILogger logger, string userId, Guid compiledLayoutId);

    [LoggerMessage(EventId = 1314, Level = LogLevel.Information, Message = "SQS message processed successfully; rawLayoutId={RawLayoutId}")]
    public static partial void SqsMessageProcessedSuccessfully(this ILogger logger, Guid rawLayoutId);

    [LoggerMessage(EventId = 1315, Level = LogLevel.Error, Message = "Failed to process SQS message; rawLayoutId={RawLayoutId} receiptHandle={ReceiptHandle}")]
    public static partial void ErrorProcessingSqsMessage(this ILogger logger, Guid rawLayoutId, string receiptHandle, Exception exception);

    [LoggerMessage(EventId = 1316, Level = LogLevel.Error, Message = "SQS message arrived in DLQ; rawLayoutId={RawLayoutId} approximateReceiveCount={ApproximateReceiveCount}")]
    public static partial void SqsMessageArrivedInDlq(this ILogger logger, Guid rawLayoutId, int approximateReceiveCount);

    [LoggerMessage(EventId = 1317, Level = LogLevel.Error, Message = "SQS polling error; will retry")]
    public static partial void SqsPollingError(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1318, Level = LogLevel.Error, Message = "Raw layout not found; rawLayoutId={RawLayoutId}")]
    public static partial void RawLayoutNotFound(this ILogger logger, Guid rawLayoutId);

    [LoggerMessage(EventId = 1319, Level = LogLevel.Information, Message = "SQS trigger enqueued; rawLayoutId={RawLayoutId} queueUrl={QueueUrl}")]
    public static partial void SqsTriggerEnqueued(this ILogger logger, Guid rawLayoutId, string queueUrl);

    [LoggerMessage(EventId = 1320, Level = LogLevel.Error, Message = "Failed to enqueue SQS trigger; rawLayoutId={RawLayoutId}")]
    public static partial void ErrorEnqueuingSqsTrigger(this ILogger logger, Guid rawLayoutId, Exception exception);

    [LoggerMessage(EventId = 1321, Level = LogLevel.Information, Message = "Validation result written back to raw layout; rawLayoutId={RawLayoutId}")]
    public static partial void ValidationResultWrittenBack(this ILogger logger, Guid rawLayoutId);

    [LoggerMessage(EventId = 1322, Level = LogLevel.Error, Message = "SQS polling error on unrecognized message; receiptHandle={ReceiptHandle}")]
    public static partial void SqsUnrecognizedMessageError(this ILogger logger, string receiptHandle, Exception exception);
}
