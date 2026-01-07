using AdaptiveRemote.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Configuration;

internal static class LoggingHostBuilderExtensions
{
    internal static IHostBuilder OptionallyAddFileLogging(this IHostBuilder builder)
        => builder.ConfigureLogging((context, logging) =>
        {
            LoggingSettings settings =
                context.Configuration.GetSection(SettingsKeys.Logging).Get<LoggingSettings>()
                ?? new LoggingSettings();
            if (settings.FilePath is not null)
            {
                logging.AddProvider(new FileLoggerProvider(settings.FilePath));
            }
        });
}
