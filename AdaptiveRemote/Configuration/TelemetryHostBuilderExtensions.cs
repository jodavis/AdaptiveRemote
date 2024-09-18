using AdaptiveRemote.Models;
using AdaptiveRemote.Utilities;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AdaptiveRemote.Configuration;

internal static class TelemetryHostBuilderExtensions
{
    public static IHostBuilder ConfigureTelemetry(this IHostBuilder builder)
        => builder
            .ConfigureServices((context, services) => services.ConfigureTelemetry(context.GetTelemetrySettings()))
            .ConfigureLogging((context, logging) => logging.ConfigureTelemetry(context.GetTelemetrySettings()));

    private static IServiceCollection ConfigureTelemetry(this IServiceCollection services, TelemetrySettings settings)
        => services
            .AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                if (settings.Publish)
                {
                    tracing.AddAzureMonitorTraceExporter(config =>
                    {
                        config.ConnectionString = settings.ConnectionString
                            ?? throw Errors.Telemetry_SettingRequiredToPublish(nameof(TelemetrySettings.ConnectionString));
                        config.Credential = new DefaultAzureCredential();
                    });
                }
            })
            .Services;

    private static ILoggingBuilder ConfigureTelemetry(this ILoggingBuilder logging, TelemetrySettings settings)
        => logging.AddOpenTelemetry(tracing =>
        {
            tracing.IncludeFormattedMessage = true;

            tracing.SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(Environment.UserName + "'s AdaptiveRemote")
                .AddEnvironmentVariableDetector());

            if (settings.LogToConsole)
            {
                tracing.AddConsoleExporter();
            }

            if (settings.Publish)
            {
                tracing.AddAzureMonitorLogExporter(configure =>
                {
                    configure.ConnectionString = settings.ConnectionString
                        ?? throw Errors.Telemetry_SettingRequiredToPublish(nameof(TelemetrySettings.ConnectionString));
                    configure.Credential = new DefaultAzureCredential();
                });
            }
        });

    private static TelemetrySettings GetTelemetrySettings(this HostBuilderContext context)
        => context.Configuration.GetSection(SettingsKeys.Telemetry)?.Get<TelemetrySettings>() ?? new();

    private class TelemetrySettings
    {
        public bool LogToConsole { get; set; } = false;

        public bool Publish { get; set; } = false;

        public string? ConnectionString { get; set; } = default;
    }
}
