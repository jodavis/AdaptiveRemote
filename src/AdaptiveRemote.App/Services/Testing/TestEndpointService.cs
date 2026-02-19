using AdaptiveRemote.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using System.Net;
using System.Net.Sockets;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Coordinates test communication and service injection during host startup.
/// Implements both ITestEndpoint (for JSON-RPC communication with tests) and 
/// ITestEndpointHooks (for coordinating with the host startup sequence).
/// </summary>
internal class TestEndpointService : ITestEndpoint, ITestEndpointHooks
{
    private readonly TestingSettings _settings;
    private readonly MessageLogger _logger;
    private readonly TaskCompletionSource<bool> _buildHostSignal = new();
    private readonly TaskCompletionSource<IServiceProvider> _servicesReadySignal = new();
    private readonly List<ServiceRegistration> _testServices = new();
    private readonly TimeSpan _startupTimeout = TimeSpan.FromMinutes(5);

    private TcpListener? _listener;
    private IHostApplicationLifetime? _lifetime;

    public TestEndpointService(TestingSettings settings, ILoggerFactory loggerFactory)
    {
        _settings = settings;
        _logger = new MessageLogger(loggerFactory.CreateLogger<TestEndpointService>());
    }

    /// <summary>
    /// Starts the TCP listener for test connections.
    /// This should be called early in startup, before the host is built.
    /// </summary>
    public void StartListening()
    {
        if (_settings.ControlPort is null)
        {
            return;
        }

        _logger.TestEndpointService_StartingTestControlEndpoint(_settings.ControlPort.Value);

        _listener = new TcpListener(IPAddress.Loopback, _settings.ControlPort.Value);
        _listener.Start();

        // Start accepting connections in background
        _ = AcceptConnectionsAsync();
    }

    private async Task AcceptConnectionsAsync()
    {
        if (_listener is null)
        {
            return;
        }

        try
        {
            while (true)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client);
                }
                catch (ObjectDisposedException)
                {
                    // Listener was stopped
                    break;
                }
                catch (Exception ex)
                {
                    _logger.TestEndpointService_StartingTestControlEndpointFailed(ex);
                    break;
                }
            }
        }
        finally
        {
            _logger.TestEndpointService_StopTestControlEndpoint();
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                JsonRpc rpc = JsonRpc.Attach(stream, this);

                _logger.TestEndpointService_ClientConnected();

                await rpc.Completion;
            }
        }
        catch (Exception ex)
        {
            _logger.TestEndpointService_ClientConnectionFailed(ex);
        }
    }

    // ITestEndpoint implementation
    public Task AddTestServiceAsync(string contractType, string serviceName, string serviceAssembly, CancellationToken cancellationToken)
    {
        _logger.TestEndpointHooksService_RegisteringTestService(serviceName, contractType);
        _testServices.Add(new ServiceRegistration(contractType, serviceName, serviceAssembly));
        return Task.CompletedTask;
    }

    public Task BuildAndRunHostAsync(CancellationToken cancellationToken)
    {
        _logger.TestEndpointHooksService_SignalingBuildHost();
        _buildHostSignal.TrySetResult(true);
        return Task.CompletedTask;
    }

    public Task StopApplicationAsync(CancellationToken cancellationToken)
    {
        _logger.TestEndpointHooksService_SignalingAbort();
        _buildHostSignal.TrySetCanceled(CancellationToken.None);
        _servicesReadySignal.TrySetCanceled(CancellationToken.None);

        _lifetime?.StopApplication();

        return Task.CompletedTask;
    }

    public async Task<ITestServiceProvider> GetTestServiceProviderAsync(CancellationToken cancellationToken)
    {
        // Wait for the host to be built and services to be available
        IServiceProvider serviceProvider = await _servicesReadySignal.Task.WaitAsync(cancellationToken);

        // Get the TestServiceProvider from DI
        return serviceProvider.GetRequiredService<ITestServiceProvider>();
    }

    // ITestEndpointHooks implementation
    public async Task InjectHostServiceAsync(IHostBuilder hostBuilder, CancellationToken cancellationToken)
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

        // Register all test services using hostBuilder.ConfigureServices
        hostBuilder.ConfigureServices(services =>
        {
            foreach (ServiceRegistration registration in _testServices)
            {
                RegisterTestService(services, registration);
            }
        });

        _logger.TestEndpointHooksService_TestServicesRegistered(_testServices.Count);
    }

    public Task ProvideServicesToTestAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _logger.TestEndpointHooksService_ProvidingServicesToTest();

        // Store the lifetime for potential shutdown requests
        _lifetime = services.GetService<IHostApplicationLifetime>();

        _servicesReadySignal.TrySetResult(services);
        return Task.CompletedTask;
    }

    private void RegisterTestService(IServiceCollection services, ServiceRegistration registration)
    {
        _logger.TestEndpointHooksService_LoadingTestServiceType(registration.ServiceName, registration.ServiceAssembly);

        // Load the service assembly
        System.Reflection.Assembly serviceAssembly = System.Reflection.Assembly.LoadFrom(registration.ServiceAssembly);

        // Find the contract type by name (may be in a different assembly than the service)
        Type? contractType = Type.GetType(registration.ContractType)
            ?? throw new ArgumentException($"Contract type not found: {registration.ContractType}");

        // Find the service type in the service assembly
        Type? serviceType = serviceAssembly.GetType(registration.ServiceName)
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

    public void Stop()
    {
        _listener?.Stop();
    }

    private record ServiceRegistration(string ContractType, string ServiceName, string ServiceAssembly);
}
