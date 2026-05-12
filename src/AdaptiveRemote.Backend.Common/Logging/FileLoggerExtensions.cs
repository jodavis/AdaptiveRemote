using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Backend.Common.Logging;

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath)
    {
        builder.AddProvider(new SimpleFileLoggerProvider(filePath));
        return builder;
    }
}

internal sealed class SimpleFileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public SimpleFileLoggerProvider(string filePath)
    {
        _filePath = filePath;
    }

    public ILogger CreateLogger(string categoryName) => new SimpleFileLogger(_filePath, _lock, categoryName);

    public void Dispose() { }

    private class SimpleFileLogger : ILogger
    {
        private readonly string _filePath;
        private readonly object _lock;
        private readonly string _categoryName;

        public SimpleFileLogger(string filePath, object lockObj, string categoryName)
        {
            _filePath = filePath;
            _lock = lockObj;
            _categoryName = categoryName;
        }

        IDisposable ILogger.BeginScope<TState>(TState state) => null!;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string message = $"[{DateTime.Now:O}] [{logLevel}] [{_categoryName}] {formatter(state, exception)}";
            lock (_lock)
            {
                File.AppendAllText(_filePath, message + "\n");
                if (exception != null)
                {
                    File.AppendAllText(_filePath, exception + "\n");
                }
            }
        }
    }
}
