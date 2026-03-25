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
    /// Raw command-line arguments passed to the application.
    /// Available for the host configuration pipeline.
    /// </summary>
    public string[] Args { get; }

    /// <summary>
    /// Startup configuration built from appsettings.json, environment variables, and command-line args.
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
        Args = args;

        // Build startup configuration from appsettings.json, environment variables, and command line args
        string environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        string basePath = AppContext.BaseDirectory;

        ConfigurationBuilder configBuilder = new();
        configBuilder.SetBasePath(basePath);
        configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        configBuilder.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false);
        configBuilder.AddEnvironmentVariables();
        configBuilder.AddCommandLine(args);
        StartupConfig = configBuilder.Build();

        // Create logger factory for startup processes, configured from StartupConfig
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.AddConfiguration(StartupConfig.GetSection("Logging"));

            // Add file logging if configured
            string? logFilePath = StartupConfig[SettingsKeys.Logging + ":FilePath"];
            if (!string.IsNullOrEmpty(logFilePath))
            {
                builder.AddProvider(new FileLoggerProvider(logFilePath));
            }
        });

        ViewModel = new();
        Controller = new LifecycleViewController(ViewModel);
        DiagnosticAdapter = new(Controller);

        // Check if test control port is configured
        int? controlPort = ParseControlPort();
        if (controlPort.HasValue)
        {
            // Create TestingSettings from startup config
            TestingSettings testSettings = new()
            {
                ControlPort = controlPort
            };

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

    public virtual void AddPrecreatedServices(IServiceCollection services)
    {
        services
            .AddSingleton(Controller)
            .AddSingleton(ViewModel);
    }

    private int? ParseControlPort()
    {
        string? portString = StartupConfig["test:ControlPort"];
        if (int.TryParse(portString, out int port))
        {
            return port;
        }

        return null;
    }
}
