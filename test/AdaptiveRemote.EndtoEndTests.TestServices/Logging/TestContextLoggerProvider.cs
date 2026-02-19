using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdaptiveRemote.EndtoEndTests.Logging;

public sealed class TestContextLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new();
    private readonly LoggerState _state;

    public TestContextLoggerProvider()
    {
        _state = new();
    }

    ILogger ILoggerProvider.CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, cn => new Logger(cn, _state));

    void IDisposable.Dispose()
        => _loggers.Clear();

    // Expose TestContext so callers (tests) can attach result files
    public TestContext? TestContext
    {
        get => _state.TestContext;
        set => _state.TestContext = value;
    }

    private sealed class LoggerState
    {
        private const int ScopeIndentLevel = 2;

        internal TestContext? TestContext { get; set; }
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
            if (_state.TestContext is TestContext context)
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

                context.Write(_state.ScopeIndent);
                context.Write(_state.TimePrefix);
                context.WriteLine($"{logLevelPrefix}[{_categoryName}] {formatter(state, exception)}");

                if (exception is not null)
                {
                    context.WriteLine(exception.ToString());
                }
            }
        }
    }

    private sealed class LoggerScope : IDisposable
    {
        private readonly LoggerState _state;
        private int _disposed = 0;

        internal LoggerScope(string? scopeName, LoggerState state)
        {
            _state = state;

            _state.Scopes.Push(scopeName);

            if (_state.TestContext is TestContext context)
            {
                context.Write(_state.ScopeIndent);
                context.WriteLine(
                    scopeName is null ? "Begin Scope" : "Begin Scope: {0}",
                    scopeName);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) == 1)
            {
                string? scopeName = _state.Scopes.Pop();
                if (_state.TestContext is TestContext context)
                {
                    context.Write(_state.ScopeIndent);
                    context.WriteLine(
                        scopeName is null ? "End Scope" : "End Scope: {0}",
                        scopeName);
                }
            }
        }
    }
}
