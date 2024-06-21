using AdaptiveRemote.Models;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace AdaptiveRemote.Services.Configuration;

internal static class TelemetryHostBuilderExtensions
{
    private const string SettingsKey = "telemetry";

    public static IHostBuilder ConfigureTelemetry(this IHostBuilder builder)
        => builder
            .ConfigureServices(services => services.ConfigureTelemetry())
            .ConfigureLogging((context, logging) => logging.ConfigureTelemetry(context.GetTelemetrySettings()));

    private static IServiceCollection ConfigureTelemetry(this IServiceCollection services)
        => services.AddOpenTelemetry().Services;

    private static ILoggingBuilder ConfigureTelemetry(this ILoggingBuilder logging, TelemetrySettings settings)
        => logging.AddOpenTelemetry(tracing =>
        {
            tracing.IncludeFormattedMessage = true;

            if (settings.LogToConsole)
            {
                tracing.AddConsoleExporter();
            }

            if (settings.Publish)
            {
                tracing.AddAzureMonitorLogExporter(configure =>
                {
                    configure.ConnectionString = settings.ConnectionString
                        ?? throw Errors.Telemetry_ConnectionStringRequired(SettingsKey, nameof(TelemetrySettings.ConnectionString));
                    configure.Credential = new VisualStudioCredential();
                });
            }
        });

    private static TelemetrySettings GetTelemetrySettings(this HostBuilderContext context)
        => context.Configuration.GetSection(SettingsKey)?.Get<TelemetrySettings>() ?? new();

    private class TelemetrySettings
    {
        public bool LogToConsole { get; set; } = false;

        public bool Publish { get; set; } = false;

        public string? ConnectionString { get; set; } = default;
    }
}
