using System.Collections.Concurrent;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests.Logging;

/// <summary>
/// This class runs in the host application process and receives log messages from
/// <see cref="HostApplicationLoggerProvider" />, adding them to the application's
/// own logs.
/// </summary>
internal class HostApplicationTestLogger : ITestLogger
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, ILogger> _categoryLoggers = new();

    public HostApplicationTestLogger(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    Task<IAsyncDisposable> ITestLogger.BeginScopeAsync(string category, string scopeName, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<IAsyncDisposable>(cancellationToken);
        }

        return Task.FromResult<IAsyncDisposable>(new RpcLogScope(GetLoggerFor(category).BeginScope(scopeName)));
    }

    Task ITestLogger.LogMessageAsync(int logLevel, string category, int eventId, string? eventName, string message, CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                LogLevel level = (LogLevel)logLevel;
                EventId id = eventId != 0 ? new EventId(eventId, eventName) : default;

#pragma warning disable CA2254 // Template should be a static expression
                GetLoggerFor(category).Log(level, id, message);
#pragma warning restore CA2254 // Template should be a static expression
            }
            catch
            {
                // TODO: Log the error
            }
        }

        return Task.CompletedTask;
    }

    private ILogger GetLoggerFor(string category) => _categoryLoggers.GetOrAdd(category, _loggerFactory.CreateLogger);

    public void Dispose()
    {
        _categoryLoggers.Clear();
    }

    private class RpcLogScope : IAsyncDisposable
    {
        private readonly IDisposable? _scope;
        public RpcLogScope(IDisposable? scope)
        {
            _scope = scope;
        }
        public ValueTask DisposeAsync()
        {
            _scope?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
