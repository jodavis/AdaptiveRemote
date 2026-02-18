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

    public AcceleratedServices(string[] args)
    {
        ViewModel = new();
        Controller = new LifecycleViewController(ViewModel);
        DiagnosticAdapter = new(Controller);

        // Check if test control port is configured
        int? controlPort = ParseControlPort(args);
        if (controlPort.HasValue)
        {
            // Create a logger for the hooks service
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            ILogger<TestEndpointHooksService> logger = loggerFactory.CreateLogger<TestEndpointHooksService>();

            TestEndpointHooksService hooksService = new(logger);
            TestEndpoint = hooksService;

            // Store the hooks service so it can be injected into TestEndpointService
            _testEndpointHooksService = hooksService;
        }
        else
        {
            TestEndpoint = new DisabledTestEndpointHooks();
            _testEndpointHooksService = null;
        }

        Controller.SetPhase(LifecyclePhase.Waiting);
    }

    private readonly TestEndpointHooksService? _testEndpointHooksService;

    public virtual void AddPrecreatedServices(IServiceCollection services)
    {
        services
            .AddSingleton(Controller)
            .AddSingleton(ViewModel);

        // If we have a test endpoint hooks service, register it so TestEndpointService can use it
        if (_testEndpointHooksService is not null)
        {
            services.AddSingleton(_testEndpointHooksService);
        }
    }

    private static int? ParseControlPort(string[] args)
    {
        // Build a minimal configuration to parse the test:ControlPort setting
        ConfigurationBuilder configBuilder = new();
        IConfigurationRoot config = configBuilder
            .AddCommandLine(args)
            .Build();

        string? portString = config["test:ControlPort"];
        if (int.TryParse(portString, out int port))
        {
            return port;
        }

        return null;
    }
}
