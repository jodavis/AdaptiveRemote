using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Backend.RawLayoutService.Logging;

/// <summary>
/// Centralized logging messages for RawLayoutService.
/// All log messages MUST be defined here as [LoggerMessage] source-generated methods.
/// Event ID ranges:
///   1200-1299: RawLayoutService
/// </summary>
public static partial class MessageLogger
{
    [LoggerMessage(EventId = 1200, Level = LogLevel.Information, Message = "RawLayoutService starting")]
    public static partial void ServiceStarting(this ILogger logger);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Information, Message = "RawLayoutService started successfully on {ListenAddress}")]
    public static partial void ServiceStarted(this ILogger logger, string listenAddress);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Information, Message = "GET /layouts/raw request received for userId={UserId}")]
    public static partial void ListRawLayoutsRequested(this ILogger logger, string userId);

    [LoggerMessage(EventId = 1203, Level = LogLevel.Information, Message = "GET /layouts/raw/{LayoutId} request received for userId={UserId}")]
    public static partial void GetRawLayoutRequested(this ILogger logger, string userId, Guid layoutId);

    [LoggerMessage(EventId = 1204, Level = LogLevel.Information, Message = "POST /layouts/raw request received for userId={UserId}")]
    public static partial void CreateRawLayoutRequested(this ILogger logger, string userId);

    [LoggerMessage(EventId = 1205, Level = LogLevel.Information, Message = "PUT /layouts/raw/{LayoutId} request received for userId={UserId}")]
    public static partial void UpdateRawLayoutRequested(this ILogger logger, string userId, Guid layoutId);

    [LoggerMessage(EventId = 1206, Level = LogLevel.Information, Message = "DELETE /layouts/raw/{LayoutId} request received for userId={UserId}")]
    public static partial void DeleteRawLayoutRequested(this ILogger logger, string userId, Guid layoutId);

    [LoggerMessage(EventId = 1207, Level = LogLevel.Information, Message = "Raw layout created successfully: Id={LayoutId}")]
    public static partial void RawLayoutCreated(this ILogger logger, Guid layoutId);

    [LoggerMessage(EventId = 1208, Level = LogLevel.Information, Message = "Raw layout updated successfully: Id={LayoutId}")]
    public static partial void RawLayoutUpdated(this ILogger logger, Guid layoutId);

    [LoggerMessage(EventId = 1209, Level = LogLevel.Information, Message = "Raw layout deleted successfully: Id={LayoutId}")]
    public static partial void RawLayoutDeleted(this ILogger logger, Guid layoutId);

    [LoggerMessage(EventId = 1210, Level = LogLevel.Information, Message = "GET /health request received")]
    public static partial void HealthCheckRequested(this ILogger logger);

    [LoggerMessage(EventId = 1211, Level = LogLevel.Information, Message = "Health check successful")]
    public static partial void HealthCheckSuccessful(this ILogger logger);

    [LoggerMessage(EventId = 1212, Level = LogLevel.Error, Message = "Error retrieving raw layouts for userId={UserId}")]
    public static partial void ErrorRetrievingRawLayouts(this ILogger logger, string userId, Exception exception);

    [LoggerMessage(EventId = 1213, Level = LogLevel.Error, Message = "Error retrieving raw layout Id={LayoutId} for userId={UserId}")]
    public static partial void ErrorRetrievingRawLayout(this ILogger logger, Guid layoutId, string userId, Exception exception);

    [LoggerMessage(EventId = 1214, Level = LogLevel.Error, Message = "Error creating raw layout for userId={UserId}")]
    public static partial void ErrorCreatingRawLayout(this ILogger logger, string userId, Exception exception);

    [LoggerMessage(EventId = 1215, Level = LogLevel.Error, Message = "Error updating raw layout Id={LayoutId} for userId={UserId}")]
    public static partial void ErrorUpdatingRawLayout(this ILogger logger, Guid layoutId, string userId, Exception exception);

    [LoggerMessage(EventId = 1216, Level = LogLevel.Error, Message = "Error deleting raw layout Id={LayoutId} for userId={UserId}")]
    public static partial void ErrorDeletingRawLayout(this ILogger logger, Guid layoutId, string userId, Exception exception);

    [LoggerMessage(EventId = 1217, Level = LogLevel.Error, Message = "Error processing health check request")]
    public static partial void ErrorProcessingHealthCheck(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1218,
        Level = LogLevel.Error,
        Message = "LocalStack dependency check failed at {HealthUrl}: {FailureReason}. LocalStack is required for local development. See docs/local-dev.md for setup instructions")]
    public static partial void LocalStackDependencyUnavailable(this ILogger logger, string healthUrl, string failureReason, Exception? exception);

    [LoggerMessage(EventId = 1219, Level = LogLevel.Information, Message = "SQS trigger enqueued; rawLayoutId={RawLayoutId} queueUrl={QueueUrl}")]
    public static partial void SqsTriggerEnqueued(this ILogger logger, Guid rawLayoutId, string queueUrl);

    [LoggerMessage(EventId = 1220, Level = LogLevel.Error, Message = "Failed to enqueue SQS trigger; rawLayoutId={RawLayoutId}")]
    public static partial void ErrorEnqueuingSqsTrigger(this ILogger logger, Guid rawLayoutId, Exception exception);

    [LoggerMessage(EventId = 1221, Level = LogLevel.Information, Message = "PATCH /layouts/raw/{LayoutId}/validation-result request received")]
    public static partial void UpdateValidationResultRequested(this ILogger logger, Guid layoutId);

    [LoggerMessage(EventId = 1222, Level = LogLevel.Information, Message = "Validation result updated for raw layout Id={LayoutId}")]
    public static partial void ValidationResultUpdated(this ILogger logger, Guid layoutId);

    [LoggerMessage(EventId = 1223, Level = LogLevel.Error, Message = "Error updating validation result for raw layout Id={LayoutId}")]
    public static partial void ErrorUpdatingValidationResult(this ILogger logger, Guid layoutId, Exception exception);
}
