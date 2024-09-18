using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Configuration;

internal static class DebugLoggingHostBuilderExtensions
{
    internal static IHostBuilder AddDebugLogging(this IHostBuilder builder)
        => builder.ConfigureServices(services => services.AddDebugLogging());

    internal static IHostBuilder AddTraceLogging(this IHostBuilder builder)
        => builder.ConfigureServices(services => services.AddTraceLogging());

    internal static IServiceCollection AddDebugLogging(this IServiceCollection services)
#if DEBUG
        => services.AddLogging(builder => builder.AddProvider(new RoutingLoggerProvider(line => Debug.WriteLine(line))));
#else
        => services;
#endif

    internal static IServiceCollection AddTraceLogging(this IServiceCollection services)
#if TRACE
        => services.AddLogging(builder => builder.AddProvider(new RoutingLoggerProvider(line => Trace.WriteLine(line))));
#else
        => services;
#endif
    private class RoutingLoggerProvider : ILoggerProvider
    {
        private Action<object?> _writeLine;

        public RoutingLoggerProvider(Action<object?> writeLine)
        {
            _writeLine = writeLine;
        }

        public ILogger CreateLogger(string categoryName)
            => new RoutingLogger(categoryName, _writeLine);
        public void Dispose() => throw new NotImplementedException();
    }

    private class RoutingLogger : ILogger
    {
        private string _categoryName;
        private Action<object?> _writeLine;

        public RoutingLogger(string categoryName, Action<object?> writeLine)
        {
            _categoryName = categoryName;
            _writeLine = writeLine;
        }

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        bool ILogger.IsEnabled(LogLevel logLevel) => true;
        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _writeLine(formatter(state, exception));
        }
    }
}
