using AdaptiveRemote.Configuration;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

public class AcceleratedServices
{
    public LifecycleView ViewModel { get; }
    public ILifecycleViewController Controller { get; }
    internal DiagnosticAdapter DiagnosticAdapter { get; }
    internal ITestEndpointHooks TestEndpoint { get; }

    /// <summary>
    /// Bootstrap configuration built from appsettings.json, environment-specific appsettings,
    /// environment variables, and command-line args (in ascending override order).
    /// Available for any startup services that need settings before the host is configured.
    /// </summary>
    public IConfigurationRoot StartupConfig { get; }

    /// <summary>
    /// Logger factory for startup processes.
    /// Should only be used for startup services that log messages before the host is configured.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; }

    public AcceleratedServices(string[] args)
    {
        string env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        StartupConfig = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // Create logger factory for startup processes, respecting configured log levels and file sink
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.AddConfiguration(StartupConfig.GetSection(SettingsKeys.Logging));

            LoggingSettings loggingSettings = GetStartupSettings<LoggingSettings>(SettingsKeys.Logging);
            builder.LogToFile(loggingSettings.FilePath);
        });

        ViewModel = new();
        Controller = new LifecycleViewController(ViewModel);
        DiagnosticAdapter = new(Controller);

        // Create TestingSettings from startup config
        TestingSettings testSettings = GetStartupSettings<TestingSettings>(SettingsKeys.Testing);

        // Check if test control port is configured
        if (testSettings.ControlPort.HasValue)
        {

            // Create and start the test endpoint service
            TestEndpointService testEndpointService = new(testSettings, LoggerFactory);
            testEndpointService.StartListening();
            TestEndpoint = testEndpointService;
        }
        else
        {
            TestEndpoint = new DisabledTestEndpointHooks();
        }

        Controller.SetPhase(LifecyclePhase.Waiting);
    }

    public SettingsType GetStartupSettings<SettingsType>(string settingsKey)
        where SettingsType : class, new() 
        => StartupConfig.GetSection(settingsKey).Get<SettingsType>() ?? new();

    public virtual void AddPrecreatedServices(IServiceCollection services)
    {
        services
            .AddSingleton(Controller)
            .AddSingleton(ViewModel);
    }
}
