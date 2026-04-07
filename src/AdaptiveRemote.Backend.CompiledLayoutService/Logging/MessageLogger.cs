using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Backend.CompiledLayoutService.Logging;

/// <summary>
/// Centralized logging messages for CompiledLayoutService.
/// All log messages MUST be defined here as [LoggerMessage] source-generated methods.
/// Event ID ranges:
///   1100-1199: CompiledLayoutService
/// </summary>
public static partial class MessageLogger
{
    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "CompiledLayoutService starting")]
    public static partial void ServiceStarting(this ILogger logger);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "CompiledLayoutService started successfully on {ListenAddress}")]
    public static partial void ServiceStarted(this ILogger logger, string listenAddress);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Information, Message = "GET /layouts/compiled/active request received")]
    public static partial void GetActiveLayoutRequested(this ILogger logger);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Information, Message = "Returning active compiled layout Id={LayoutId}")]
    public static partial void ReturningActiveLayout(this ILogger logger, Guid layoutId);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Information, Message = "GET /health request received")]
    public static partial void HealthCheckRequested(this ILogger logger);

    [LoggerMessage(EventId = 1105, Level = LogLevel.Information, Message = "Health check successful")]
    public static partial void HealthCheckSuccessful(this ILogger logger);
}
