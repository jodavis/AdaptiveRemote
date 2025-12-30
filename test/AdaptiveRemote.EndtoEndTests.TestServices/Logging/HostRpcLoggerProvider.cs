using System.Collections.Concurrent;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests.Logging;

/// <summary>
/// An ILoggerProvider for tests that forwards log messages to the host process via ITestControlService when available.
/// Until the RPC connection is established, messages are buffered and flushed once a proxy is attached.
/// </summary>
internal sealed class HostRpcLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new();
    private readonly HostRpcLoggerState _state = new();

    public HostRpcLoggerProvider()
    {
    }

    public void AttachControlProxy(ITestLogger proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy, nameof(proxy));

        if (Interlocked.Exchange(ref _state.HasProxy, 1) == 1)
        {
            return;
        }

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

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (_state.LoggerProxy is not null)
            {
                string message = formatter(state, exception);
                int level = (int)logLevel;

                try
                {
                    WaitUtilities.WaitForAsyncTask(ct => _state.LoggerProxy.LogMessageAsync(level, _category, eventId.Id, eventId.Name, message, ct));
                }
                catch
                {
                    // Ignore exceptions -- make a best effort to log but don't fail the test
                }
            }
        }

        private class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();
            public void Dispose() { }
        }
    }

    private sealed class HostRpcLoggerState
    {
        public ITestLogger? LoggerProxy;
        public int HasProxy = 0;
    }

    private sealed class HostRpcLogScope
    {

    }
}
