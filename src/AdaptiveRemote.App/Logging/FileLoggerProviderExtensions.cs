using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Logging;

internal static class FileLoggerProviderExtensions
{
    private static readonly ConcurrentDictionary<string, FileLoggerProvider> _providers = new();

    internal static ILoggingBuilder LogToFile(this ILoggingBuilder builder, string? filePath)
    {
        return filePath is null
            ? builder
            : builder.AddProvider(_providers.GetOrAdd(filePath, fp => new(fp)));
    }
}
