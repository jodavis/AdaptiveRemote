using AdaptiveRemote.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Implements test endpoint hooks to coordinate test service injection during host startup.
/// This service blocks the host startup until tests have finished registering their services.
/// </summary>
internal class TestEndpointHooksService : ITestEndpointHooks
{
    private readonly MessageLogger _logger;
    private readonly TaskCompletionSource<bool> _buildHostSignal = new();
    private readonly TaskCompletionSource<IServiceProvider> _servicesReadySignal = new();
    private readonly List<ServiceRegistration> _testServices = new();
    private readonly TimeSpan _startupTimeout = TimeSpan.FromMinutes(5);

    public TestEndpointHooksService(ILogger<TestEndpointHooksService> logger)
    {
        _logger = new(logger);
    }

    /// <summary>
    /// Registers a test service to be added to the host's service collection.
    /// </summary>
    public void AddTestService(string contractType, string serviceName, string serviceAssembly)
    {
        _logger.TestEndpointHooksService_RegisteringTestService(serviceName, contractType);
        _testServices.Add(new ServiceRegistration(contractType, serviceName, serviceAssembly));
    }

    /// <summary>
    /// Signals that test service registration is complete and the host can be built.
    /// </summary>
    public void SignalBuildHost()
    {
        _logger.TestEndpointHooksService_SignalingBuildHost();
        _buildHostSignal.TrySetResult(true);
    }

    /// <summary>
    /// Signals that the host startup should be aborted.
    /// </summary>
    public void SignalAbort()
    {
        _logger.TestEndpointHooksService_SignalingAbort();
        _buildHostSignal.TrySetCanceled();
        _servicesReadySignal.TrySetCanceled();
    }

    /// <summary>
    /// Waits for test services to be ready (after the host is built).
    /// </summary>
    public Task<IServiceProvider> WaitForServicesAsync() => _servicesReadySignal.Task;

    public async Task InjectHostServiceAsync(IHostBuilder hostBuilder, IServiceCollection services, CancellationToken cancellationToken)
    {
        _logger.TestEndpointHooksService_WaitingForTestServices();

        // Wait for tests to signal that they're done registering services, or timeout
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_startupTimeout);

        try
        {
            using CancellationTokenRegistration ctRegistration = timeoutCts.Token.Register(() => _buildHostSignal.TrySetCanceled());
            await _buildHostSignal.Task;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred - proceed with startup without injected services
            _logger.TestEndpointHooksService_TestServicesRegistered(0);
            return;
        }

        // Register all test services
        foreach (ServiceRegistration registration in _testServices)
        {
            RegisterTestService(services, registration);
        }

        _logger.TestEndpointHooksService_TestServicesRegistered(_testServices.Count);
    }

    public Task ProvideServicesToTestAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        _logger.TestEndpointHooksService_ProvidingServicesToTest();
        _servicesReadySignal.TrySetResult(serviceProvider);
        return Task.CompletedTask;
    }

    private void RegisterTestService(IServiceCollection services, ServiceRegistration registration)
    {
        _logger.TestEndpointHooksService_LoadingTestServiceType(registration.ServiceName, registration.ServiceAssembly);

        Assembly assembly = Assembly.LoadFrom(registration.ServiceAssembly);

        Type? contractType = assembly.GetType(registration.ContractType)
            ?? throw new ArgumentException($"Contract type not found: {registration.ContractType}");

        Type? serviceType = assembly.GetType(registration.ServiceName)
            ?? throw new ArgumentException($"Service type not found: {registration.ServiceName}");

        if (!contractType.IsAssignableFrom(serviceType))
        {
            throw new ArgumentException(
                $"Service type {registration.ServiceName} does not implement contract {registration.ContractType}");
        }

        _logger.TestEndpointHooksService_RegisteringTestServiceInDI(registration.ServiceName, registration.ContractType);

        // Register as singleton to ensure the same instance is used throughout the application
        services.AddSingleton(contractType, serviceType);
    }

    private record ServiceRegistration(string ContractType, string ServiceName, string ServiceAssembly);
}
