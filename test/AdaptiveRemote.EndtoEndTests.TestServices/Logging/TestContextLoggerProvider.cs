using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdaptiveRemote.EndtoEndTests.Logging;

public sealed class TestContextLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new();
    private readonly LoggerState _state;

    public TestContextLoggerProvider(TestContext testContext)
    {
        _state = new()
        {
            TestContext = testContext
        };
    }

    ILogger ILoggerProvider.CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, cn => new Logger(cn, _state));

    void IDisposable.Dispose()
        => _loggers.Clear();

    // Expose TestContext so callers (tests) can attach result files
    internal TestContext TestContext => _state.TestContext;

    private sealed class LoggerState
    {
        private const int ScopeIndentLevel = 2;

        internal required TestContext TestContext { get; init; }
        internal DateTime StartTime { get; } = DateTime.Now;
        internal Stack<string?> Scopes { get; } = new();
        internal string ScopeIndent => new string(' ', Scopes.Count << ScopeIndentLevel);
        internal string TimePrefix => $"[{(DateTime.Now - StartTime).TotalSeconds:0.000}s] ";
    }

    private sealed class Logger : ILogger
    {
        private readonly string _categoryName;
        private readonly LoggerState _state;

        internal Logger(string categoryName, LoggerState state)
        {
            _categoryName = categoryName;
            _state = state;
        }

        bool ILogger.IsEnabled(LogLevel logLevel) => true;

        IDisposable? ILogger.BeginScope<TState>(TState state) => new LoggerScope(state?.ToString(), _state);

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string logLevelPrefix = logLevel switch
            {
                LogLevel.Critical => "crit: ",
                LogLevel.Error => "err: ",
                LogLevel.Warning => "warn: ",
                LogLevel.Debug => "dbug: ",
                LogLevel.Trace => "trce: ",
                _ => "",
            };

            _state.TestContext.Write(_state.ScopeIndent);
            _state.TestContext.Write(_state.TimePrefix);
            _state.TestContext.WriteLine($"{logLevelPrefix}[{_categoryName}] {formatter(state, exception)}");
        }
    }

    private sealed class LoggerScope : IDisposable
    {
        private readonly LoggerState _state;
        private int _disposed = 0;

        internal LoggerScope(string? scopeName, LoggerState state)
        {
            _state = state;

            _state.TestContext.Write(_state.ScopeIndent);
            _state.TestContext.WriteLine(
                scopeName is null ? "Begin Scope" : "Begin Scope: {0}",
                scopeName);
            _state.Scopes.Push(scopeName);
        }

        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) == 1)
            {
                string? scopeName = _state.Scopes.Pop();
                _state.TestContext.Write(_state.ScopeIndent);
                _state.TestContext.WriteLine(
                    scopeName is null ? "End Scope" : "End Scope: {0}",
                    scopeName);
            }
        }
    }
}
