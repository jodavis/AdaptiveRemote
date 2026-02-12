using AdaptiveRemote.Models;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

public class AcceleratedServices
{
    private readonly string[] _args;
    private TestEndpointCoordinator? _testCoordinator;

    public LifecycleView ViewModel { get; }
    public ILifecycleViewController Controller { get; }
    internal DiagnosticAdapter DiagnosticAdapter { get; }

    public AcceleratedServices(string[] args)
    {
        ViewModel = new();
        Controller = new LifecycleViewController(ViewModel);
        DiagnosticAdapter = new(Controller);

        Controller.SetPhase(LifecyclePhase.Waiting);

        _args = args;
    }

    public void ConfigureHost(IHostBuilder hostBuilder)
    {
        hostBuilder
            .ConfigureAppSettings(_args)
            .ConfigureApp()
            .ConfigureServices(AddPrecreatedServices);
    }

    /// <summary>
    /// Initializes test coordinator if in test mode and waits for test initialization.
    /// Should be called before building the host.
    /// </summary>
    public void InitializeTestCoordinator(IConfiguration configuration, ILoggerFactory? loggerFactory = null)
    {
        ILogger<TestEndpointCoordinator>? logger = loggerFactory?.CreateLogger<TestEndpointCoordinator>();
        _testCoordinator = new TestEndpointCoordinator(configuration, logger);

        if (_testCoordinator.IsTestModeEnabled)
        {
            // Coordinator will be signaled by test via RPC
            // The WaitForTestInitialization will be called later before Build()
        }
    }

    /// <summary>
    /// Waits for test initialization if in test mode.
    /// Returns true if ready to continue, false if timeout.
    /// </summary>
    public bool WaitForTestInitialization()
    {
        if (_testCoordinator == null)
        {
            return true; // No test coordinator, continue immediately
        }

        return _testCoordinator.WaitForTestInitialization();
    }

    /// <summary>
    /// Applies pending test service registrations.
    /// Should be called before adding other services.
    /// </summary>
    public void ApplyTestServiceRegistrations(IServiceCollection services)
    {
        _testCoordinator?.ApplyServiceRegistrations(services);
    }

    /// <summary>
    /// Creates a TestEndpointService for early initialization (before DI is fully configured).
    /// </summary>
    public ITestEndpoint? CreateEarlyTestEndpoint(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        if (_testCoordinator == null)
        {
            return null;
        }

        int? controlPort = configuration.GetValue<int?>("test:ControlPort");
        if (!controlPort.HasValue)
        {
            return null;
        }

        TestingSettings settings = new() { ControlPort = controlPort.Value };
        ILogger<TestEndpointService> logger = loggerFactory.CreateLogger<TestEndpointService>();

        return new TestEndpointService(
            Microsoft.Extensions.Options.Options.Create(settings),
            null, // ScopeProvider will be null initially, but service creation happens later when DI is ready
            logger,
            _testCoordinator);
    }

    protected virtual void AddPrecreatedServices(IServiceCollection services)
    {
        // Apply test service registrations first
        ApplyTestServiceRegistrations(services);

        // Add accelerated services
        services
            .AddSingleton(Controller)
            .AddSingleton(ViewModel);

        // Add test coordinator if available
        if (_testCoordinator != null)
        {
            services.AddSingleton(_testCoordinator);
        }
    }
}
