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
    /// Command line configuration parsed from arguments.
    /// Available for any startup services that need command line settings.
    /// </summary>
    public IConfigurationRoot CommandLineConfig { get; }

    /// <summary>
    /// Logger factory for startup processes.
    /// Should only be used for startup services that log messages before the host is configured.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; }

    public AcceleratedServices(string[] args)
    {
        // Parse command line configuration early
        ConfigurationBuilder configBuilder = new();
        CommandLineConfig = configBuilder
            .AddCommandLine(args)
            .Build();

        // Create logger factory for startup processes
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());

        ViewModel = new();
        Controller = new LifecycleViewController(ViewModel);
        DiagnosticAdapter = new(Controller);

        // Check if test control port is configured
        TestingSettings? testSettings = CommandLineConfig.GetSection("test").Get<TestingSettings>();
        if (testSettings?.ControlPort is not null)
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

    public virtual void AddPrecreatedServices(IServiceCollection services) =>
        services
            .AddSingleton(Controller)
            .AddSingleton(ViewModel);
}
