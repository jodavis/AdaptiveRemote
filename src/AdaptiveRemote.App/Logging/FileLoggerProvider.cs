using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _writeLock = new();

    public FileLoggerProvider(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, cn => new FileLogger(cn, _writer, _writeLock));

    public void Dispose()
    {
        try
        {
            _writer.Dispose();
        }
        catch { }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly StreamWriter _writer;
        private readonly object _writeLock;

        public FileLogger(string category, StreamWriter writer, object writeLock)
        {
            _category = category;
            _writer = writer;
            _writeLock = writeLock;
        }

        public IDisposable BeginScope<TState>(TState state) => null!;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            string line = $"[{DateTime.Now:O}] {logLevel} [{_category}] {message}";
            lock (_writeLock)
            {
                _writer.WriteLine(line);
                if (exception is not null)
                {
                    _writer.WriteLine(exception.ToString());
                }
            }
        }
    }
}
