using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Coordinates test endpoint initialization and service registration before host startup.
/// Blocks host build until test connection is established and services are registered.
/// </summary>
public class TestEndpointCoordinator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TestEndpointCoordinator>? _logger;
    private readonly ManualResetEventSlim _startupGate = new(initialState: false);
    private readonly ConcurrentQueue<ServiceRegistration> _pendingRegistrations = new();
    private readonly TimeSpan _connectionTimeout = TimeSpan.FromSeconds(30);

    public TestEndpointCoordinator(IConfiguration configuration, ILogger<TestEndpointCoordinator>? logger = null)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gets whether test mode is enabled (test:ControlPort is configured).
    /// </summary>
    public bool IsTestModeEnabled => _configuration.GetValue<int?>("test:ControlPort").HasValue;

    /// <summary>
    /// Registers a service to be added to the DI container.
    /// </summary>
    public void RegisterService(string serviceTypeName, string implementationTypeName, string assemblyPath)
    {
        _logger?.LogInformation("Registering test service: {ServiceType} -> {ImplementationType}", 
            serviceTypeName, implementationTypeName);
        
        _pendingRegistrations.Enqueue(new ServiceRegistration(serviceTypeName, implementationTypeName, assemblyPath));
    }

    /// <summary>
    /// Signals that test initialization is complete and startup can continue.
    /// </summary>
    public void ContinueStartup()
    {
        _logger?.LogInformation("Test initialization complete, continuing startup");
        _startupGate.Set();
    }

    /// <summary>
    /// Blocks until test initialization is complete or timeout occurs.
    /// Returns true if successful, false if timeout.
    /// </summary>
    public bool WaitForTestInitialization()
    {
        if (!IsTestModeEnabled)
        {
            return true; // Not in test mode, continue immediately
        }

        _logger?.LogInformation("Waiting for test initialization (timeout: {Timeout})", _connectionTimeout);
        
        bool success = _startupGate.Wait(_connectionTimeout);
        
        if (!success)
        {
            _logger?.LogError("Test initialization timeout after {Timeout}", _connectionTimeout);
        }
        
        return success;
    }

    /// <summary>
    /// Applies all pending service registrations to the service collection.
    /// </summary>
    public void ApplyServiceRegistrations(IServiceCollection services)
    {
        while (_pendingRegistrations.TryDequeue(out ServiceRegistration? registration))
        {
            _logger?.LogInformation("Applying service registration: {ServiceType} -> {ImplementationType}",
                registration.ServiceTypeName, registration.ImplementationTypeName);

            try
            {
                Assembly assembly = Assembly.LoadFrom(registration.AssemblyPath);
                
                Type? serviceType = Type.GetType(registration.ServiceTypeName) 
                    ?? assembly.GetType(registration.ServiceTypeName);
                
                Type? implementationType = assembly.GetType(registration.ImplementationTypeName);

                if (serviceType == null)
                {
                    _logger?.LogError("Service type not found: {ServiceType}", registration.ServiceTypeName);
                    continue;
                }

                if (implementationType == null)
                {
                    _logger?.LogError("Implementation type not found: {ImplementationType}", 
                        registration.ImplementationTypeName);
                    continue;
                }

                services.AddSingleton(serviceType, implementationType);
                
                _logger?.LogInformation("Successfully registered {ServiceType} -> {ImplementationType}",
                    serviceType.Name, implementationType.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to register service: {ServiceType} -> {ImplementationType}",
                    registration.ServiceTypeName, registration.ImplementationTypeName);
            }
        }
    }

    private record ServiceRegistration(string ServiceTypeName, string ImplementationTypeName, string AssemblyPath);
}
