using PolyType;
using StreamJsonRpc;

namespace AdaptiveRemote.Services.Testing;

[RpcMarshalable]
[JsonRpcContract]
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface ITestLogger : IDisposable
{
    /// <summary>
    /// Logs a formatted message on the host side using the host application's logging infrastructure.
    /// This corresponds to <see cref="Microsoft.Extensions.Logging.ILogger.Log{TState}(Microsoft.Extensions.Logging.LogLevel, Microsoft.Extensions.Logging.EventId, TState, System.Exception?, System.Func{TState, System.Exception?, string})"/> semantics
    /// but is expressed as an RPC-friendly operation.
    /// </summary>
    /// <param name="logLevel">Integer representation of <see cref="Microsoft.Extensions.Logging.LogLevel"/> describing the severity of the message.</param>
    /// <param name="category">The logger category name (typically the fully-qualified type name) to be used on the host.</param>
    /// <param name="eventId">Numeric event id associated with the message (maps to <see cref="Microsoft.Extensions.Logging.EventId.Id"/>).</param>
    /// <param name="eventName">Optional event name associated with the message (maps to <see cref="Microsoft.Extensions.Logging.EventId.Name"/>).</param>
    /// <param name="message">The already-formatted message text to be written by the host logger.</param>
    /// <returns>A task that completes when the remote host has accepted the log message; fire-and-forget callers may ignore the returned task.</returns>
    Task LogMessageAsync(int logLevel, string category, int eventId, string? eventName, string message, CancellationToken cancellationToken);

    /// <summary>
    /// Begins a logical logging scope on the host for the specified category and scope name.
    /// This mirrors <see cref="Microsoft.Extensions.Logging.ILogger.BeginScope{TState}(TState)"/> semantics but is implemented as an RPC call that returns
    /// an <see cref="IAsyncDisposable"/> which should be disposed when the scope ends.
    /// </summary>
    /// <param name="category">The logger category for which the scope applies.</param>
    /// <param name="scopeName">A string representation of the scope state/value. The host may format this value into its logs.</param>
    /// <returns>An <see cref="IAsyncDisposable"/> that, when disposed, will end the scope on the host.</returns>
    Task<IAsyncDisposable> BeginScopeAsync(string category, string scopeName, CancellationToken cancellationToken);
}
