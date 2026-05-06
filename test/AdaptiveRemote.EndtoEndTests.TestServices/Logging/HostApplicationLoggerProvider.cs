using System.Collections.Concurrent;
using AdaptiveRemote.Services.Testing;
using AdaptiveRemote.TestUtilities;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests.Logging;

/// <summary>
/// An ILoggerProvider for tests that forwards log messages to the host process via 
/// ITestLogger when available.
/// </summary>
/// <remarks>
/// This class runs in the test process and relays log messages to <see cref="HostApplicationTestLogger" />
/// in the host application process.
/// </remarks>
public sealed class HostApplicationLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new();
    private readonly HostRpcLoggerState _state = new();

    internal void AttachTestLoggerProxy(ITestLogger proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy, nameof(proxy));

        _state.LoggerProxy = proxy;
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, cn => new HostRpcLogger(cn, _state));

    public void Dispose() => _loggers.Clear();

    private sealed class HostRpcLogger : ILogger
    {
        private readonly string _category;
        private readonly HostRpcLoggerState _state;

        internal HostRpcLogger(string category, HostRpcLoggerState state)
        {
            _category = category;
            _state = state;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            if (_state.LoggerProxy is not null)
            {
                string? scopeName = state?.ToString();
                try
                {
                    // Synchronously wait for the remote BeginScopeAsync to complete and return
                    IAsyncDisposable remoteScope = WaitHelpers.WaitForAsyncTask(ct => _state.LoggerProxy.BeginScopeAsync(_category, scopeName ?? string.Empty, ct));

                    return new RemoteScope(remoteScope);
                }
                catch
                {
                    // If anything goes wrong, fall back to a local null scope so logging doesn't throw
                    return NullScope.Instance;
                }
            }

            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (_state.LoggerProxy is not null)
            {
                string message = formatter(state, exception);
                int level = (int)logLevel;

                try
                {
                    WaitHelpers.WaitForAsyncTask(ct => _state.LoggerProxy.LogMessageAsync(level, _category, eventId.Id, eventId.Name, message, ct));
                }
                catch
                {
                    // Ignore exceptions -- make a best effort to log but don't fail the test
                }
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();
            public void Dispose() { }
        }

        private sealed class RemoteScope : IDisposable
        {
            private readonly IAsyncDisposable _remote;
            private int _disposed;

            public RemoteScope(IAsyncDisposable remote)
            {
                _remote = remote;
            }

            public void Dispose()
            {
                if (Interlocked.Increment(ref _disposed) == 1)
                {
                    try
                    {
                        // Synchronously wait for the remote scope to be disposed
                        WaitHelpers.WaitForAsyncTask(ct => _remote.DisposeAsync().AsTask());
                    }
                    catch
                    {
                        // swallow exceptions to avoid throwing from Dispose
                    }
                }
            }
        }
    }

    private sealed class HostRpcLoggerState
    {
        public ITestLogger? LoggerProxy;
    }
}
