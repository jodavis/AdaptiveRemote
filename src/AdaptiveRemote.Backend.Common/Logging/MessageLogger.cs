using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Backend.Common.Logging;

/// <summary>
/// Centralized logging messages for CompiledLayoutService.
/// All log messages MUST be defined here as [LoggerMessage] source-generated methods.
/// Event ID ranges:
///   1100-1199: CompiledLayoutService
/// </summary>
public static partial class MessageLogger
{
    // Common service messages
    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "{ServiceName} starting")]
    public static partial void ServiceStarting(this ILogger logger, string serviceName);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "{ServiceName} started successfully on {ListenAddress}")]
    public static partial void ServiceStarted(this ILogger logger, string serviceName, string listenAddress);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Information, Message = "{Method} {Path} request received for userId={UserId}")]
    public static partial void AuthenticatedRequestStarted(this ILogger logger, string method, string path, string userId);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Information, Message = "{Method} {Path} request received")]
    public static partial void UnauthenticatedRequestStarted(this ILogger logger, string method, string path);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Information, Message = "{Method} {Path} request handled")]
    public static partial void RequestHandled(this ILogger logger, string method, string path);

    [LoggerMessage(EventId = 1105, Level = LogLevel.Information, Message = "Health check successful")]
    public static partial void HealthCheckSuccessful(this ILogger logger);

    [LoggerMessage(EventId = 1106, Level = LogLevel.Error, Message = "Error processing health check request")]
    public static partial void ErrorProcessingHealthCheck(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1107,
        Level = LogLevel.Error,
        Message = "LocalStack dependency check failed at {HealthUrl}: {FailureReason}. LocalStack is required for local development. See docs/local-dev.md for setup instructions")]
    public static partial void LocalStackDependencyUnavailable(this ILogger logger, string healthUrl, string failureReason, Exception? exception);

    // CompiledLayoutService-specific messages
    [LoggerMessage(EventId = 1301, Level = LogLevel.Information, Message = "Returning active compiled layout Id={LayoutId}")]
    public static partial void ReturningActiveLayout(this ILogger logger, Guid layoutId);

    [LoggerMessage(EventId = 1303, Level = LogLevel.Error, Message = "Error retrieving active layout for userId={UserId}")]
    public static partial void ErrorRetrievingActiveLayout(this ILogger logger, string userId, Exception exception);

    // RawLayoutService-specific messages
    [LoggerMessage(EventId = 1201, Level = LogLevel.Information, Message = "Raw layout created successfully: Id={LayoutId}")]
    public static partial void RawLayoutCreated(this ILogger logger, Guid layoutId);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Information, Message = "Raw layout updated successfully: Id={LayoutId}")]
    public static partial void RawLayoutUpdated(this ILogger logger, Guid layoutId);

    [LoggerMessage(EventId = 1203, Level = LogLevel.Information, Message = "Raw layout deleted successfully: Id={LayoutId}")]
    public static partial void RawLayoutDeleted(this ILogger logger, Guid layoutId);

    [LoggerMessage(EventId = 1204, Level = LogLevel.Error, Message = "Error retrieving raw layouts for userId={UserId}")]
    public static partial void ErrorRetrievingRawLayouts(this ILogger logger, string userId, Exception exception);

    [LoggerMessage(EventId = 1205, Level = LogLevel.Error, Message = "Error retrieving raw layout Id={LayoutId} for userId={UserId}")]
    public static partial void ErrorRetrievingRawLayout(this ILogger logger, Guid layoutId, string userId, Exception exception);

    [LoggerMessage(EventId = 1206, Level = LogLevel.Error, Message = "Error creating raw layout for userId={UserId}")]
    public static partial void ErrorCreatingRawLayout(this ILogger logger, string userId, Exception exception);

    [LoggerMessage(EventId = 1207, Level = LogLevel.Error, Message = "Error updating raw layout Id={LayoutId} for userId={UserId}")]
    public static partial void ErrorUpdatingRawLayout(this ILogger logger, Guid layoutId, string userId, Exception exception);

    [LoggerMessage(EventId = 1208, Level = LogLevel.Error, Message = "Error deleting raw layout Id={LayoutId} for userId={UserId}")]
    public static partial void ErrorDeletingRawLayout(this ILogger logger, Guid layoutId, string userId, Exception exception);

    [LoggerMessage(EventId = 1209, Level = LogLevel.Information, Message = "SQS trigger enqueued; rawLayoutId={RawLayoutId} queueUrl={QueueUrl}")]
    public static partial void SqsTriggerEnqueued(this ILogger logger, Guid rawLayoutId, string queueUrl);

    [LoggerMessage(EventId = 1210, Level = LogLevel.Error, Message = "Failed to enqueue SQS trigger; rawLayoutId={RawLayoutId}")]
    public static partial void ErrorEnqueuingSqsTrigger(this ILogger logger, Guid rawLayoutId, Exception exception);

    [LoggerMessage(EventId = 1211, Level = LogLevel.Information, Message = "Validation result updated for raw layout Id={LayoutId}")]
    public static partial void ValidationResultUpdated(this ILogger logger, Guid layoutId);

    [LoggerMessage(EventId = 1212, Level = LogLevel.Error, Message = "Error updating validation result for raw layout Id={LayoutId}")]
    public static partial void ErrorUpdatingValidationResult(this ILogger logger, Guid layoutId, Exception exception);

    // LayoutProcessingService-specific messages
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

    // LayoutCompilerService-specific messages
    [LoggerMessage(EventId = 1800, Level = LogLevel.Information, Message = "Compilation started; rawLayoutId={RawLayoutId}")]
    public static partial void CompilationStarted(this ILogger logger, Guid rawLayoutId);

    [LoggerMessage(EventId = 1801, Level = LogLevel.Information, Message = "Compilation completed; rawLayoutId={RawLayoutId}")]
    public static partial void CompilationCompleted(this ILogger logger, Guid rawLayoutId);

    [LoggerMessage(EventId = 1802, Level = LogLevel.Information, Message = "Preview compilation started")]
    public static partial void PreviewCompilationStarted(this ILogger logger);

    [LoggerMessage(EventId = 1803, Level = LogLevel.Information, Message = "Preview compilation completed")]
    public static partial void PreviewCompilationCompleted(this ILogger logger);

}
