using AdaptiveRemote.Logging;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StreamJsonRpc;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Provides a test control endpoint via TCP/JSON-RPC for E2E testing.
/// Enabled when --test:ControlPort argument is provided.
/// </summary>
internal class TestEndpointService : BackgroundService, ITestEndpoint
{
    private readonly TestingSettings _settings;
    private readonly IApplicationScopeProvider _scopeProvider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly MessageLogger _logger;
    private readonly TestEndpointHooksService? _hooksService;
    private TcpListener? _listener;

    public TestEndpointService(
        IOptions<TestingSettings> settings,
        IApplicationScopeProvider scopeProvider,
        IHostApplicationLifetime lifetime,
        ILogger<TestEndpointService> logger,
        TestEndpointHooksService? hooksService = null)
    {
        _settings = settings.Value;
        _scopeProvider = scopeProvider;
        _lifetime = lifetime;
        _logger = new(logger);
        _hooksService = hooksService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings.ControlPort is null)
        {
            // Test control endpoint not requested
            return;
        }

        _logger.TestEndpointService_StartingTestControlEndpoint(_settings.ControlPort.Value);

        _listener = new TcpListener(IPAddress.Loopback, _settings.ControlPort.Value);
        _listener.Start();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(stoppingToken);
                    _ = HandleClientAsync(client, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Listener was stopped
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Listener was stopped
                    break;
                }
                catch (Exception ex)
                {
                    _logger.TestEndpointService_StartingTestControlEndpointFailed(ex);
                }
            }
        }
        finally
        {
            _logger.TestEndpointService_StopTestControlEndpoint();
            _listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken stoppingToken)
    {
        try
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                JsonRpc rpc = JsonRpc.Attach(stream, this);

                stoppingToken.Register(rpc.Dispose);

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
        if (_hooksService is null)
        {
            throw new InvalidOperationException("Test hooks service is not available. Ensure the test endpoint is properly configured.");
        }

        _hooksService.AddTestService(contractType, serviceName, serviceAssembly);
        return Task.CompletedTask;
    }

    public Task BuildAndRunHostAsync(CancellationToken cancellationToken)
    {
        if (_hooksService is null)
        {
            throw new InvalidOperationException("Test hooks service is not available. Ensure the test endpoint is properly configured.");
        }

        _hooksService.SignalBuildHost();
        return Task.CompletedTask;
    }

    public Task StopApplicationAsync(CancellationToken cancellationToken)
    {
        _hooksService?.SignalAbort();

        _lifetime.StopApplication();
        return Task.CompletedTask;
    }

    public async Task<ITestServiceProvider> GetTestServiceProviderAsync(CancellationToken cancellationToken)
    {
        if (_hooksService is null)
        {
            throw new InvalidOperationException("Test hooks service is not available. Ensure the test endpoint is properly configured.");
        }

        // Wait for the host to be built and services to be available
        await _hooksService.WaitForServicesAsync().WaitAsync(cancellationToken);

        // Return self as the test service provider
        return new TestServiceProviderImpl(_scopeProvider, _logger);
    }

    // Inner class that implements ITestServiceProvider
    private class TestServiceProviderImpl : ITestServiceProvider
    {
        private readonly IApplicationScopeProvider _scopeProvider;
        private readonly MessageLogger _logger;

        public TestServiceProviderImpl(IApplicationScopeProvider scopeProvider, MessageLogger logger)
        {
            _scopeProvider = scopeProvider;
            _logger = logger;
        }

        public Task<IApplicationTestService> CreateTestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
            => CreateRemotableServiceAsync<IApplicationTestService>(assemblyPath, typeName, cancellationToken);

        public Task<ITestLogger> CreateTestLoggerAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
            => CreateRemotableServiceAsync<ITestLogger>(assemblyPath, typeName, cancellationToken);

        public Task<IUITestService> CreateUITestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
            => CreateRemotableServiceAsync<IUITestService>(assemblyPath, typeName, cancellationToken);

        private async Task<ServiceType> CreateRemotableServiceAsync<ServiceType>(string assemblyPath, string typeName, CancellationToken cancellationToken)
            where ServiceType : class
        {
            _logger.TestEndpointService_LoadingTestService(typeName, assemblyPath);

            Assembly assembly = Assembly.LoadFrom(assemblyPath);

            Type? serviceType = assembly.GetType(typeName)
                ?? throw new ArgumentException($"Type not found: {typeName}", nameof(typeName));

            if (!typeof(ServiceType).IsAssignableFrom(serviceType))
            {
                _logger.TestEndpointService_ServiceTypeIncompatible(typeName, typeof(ServiceType).FullName);
                throw new ArgumentException($"Type {typeName} does not implement {typeof(ServiceType).FullName}", nameof(typeName));
            }

            // Store the type to instantiate later within each scoped invocation
            _logger.TestEndpointService_LoadingTestServiceSucceeded();

            // Create the test service within the application scope so it gets access to scoped services
            ServiceType? testService = null;
            await _scopeProvider.InvokeInScopeAsync((scopedProvider, ct) =>
            {
                testService = (ServiceType)ActivatorUtilities.CreateInstance(scopedProvider, serviceType);
                return Task.CompletedTask;
            }, cancellationToken);

            return testService ?? throw new InvalidOperationException("Failed to create test service instance");
        }
    }
}
