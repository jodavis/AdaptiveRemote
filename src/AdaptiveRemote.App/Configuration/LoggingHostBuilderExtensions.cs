using AdaptiveRemote.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

internal static class LoggingHostBuilderExtensions
{
    internal static IHostBuilder OptionallyAddFileLogging(this IHostBuilder builder)
        => builder.ConfigureLogging((context, logging) =>
        {
            LoggingSettings settings = context.Configuration.GetSection(SettingsKeys.Logging).Get<LoggingSettings>()
                ?? new LoggingSettings();
            logging.LogToFile(settings.FilePath);
        });
}
