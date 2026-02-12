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
/// In test mode with early listener, this service doesn't create its own listener -
/// the RPC calls are forwarded from EarlyTestEndpointListener.
/// </summary>
internal class TestEndpointService : BackgroundService, ITestEndpoint
{
    private readonly TestingSettings _settings;
    private readonly IApplicationScopeProvider? _scopeProvider;
    private readonly TestEndpointCoordinator? _coordinator;
    private readonly MessageLogger _logger;
    private TcpListener? _listener;

    public TestEndpointService(
        IOptions<TestingSettings> settings,
        IApplicationScopeProvider? scopeProvider,
        ILogger<TestEndpointService> logger,
        TestEndpointCoordinator? coordinator = null)
    {
        _settings = settings.Value;
        _scopeProvider = scopeProvider;
        _coordinator = coordinator;
        _logger = new(logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings.ControlPort is null)
        {
            // Test control endpoint not requested
            return;
        }

        // In test mode with EarlyTestEndpointListener, we don't need to do anything here
        // The RPC calls are being forwarded from the early listener
        // Just wait to be cancelled
        if (_coordinator != null)
        {
            _logger.TestEndpointService_StartingTestControlEndpoint(_settings.ControlPort.Value);

            try
            {
                // Just wait for cancellation
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            finally
            {
                _logger.TestEndpointService_StopTestControlEndpoint();
            }

            return;
        }

        // No coordinator - start our own listener (non-test-mode or fallback)
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

    public Task<IApplicationTestService> CreateTestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
        => CreateRemotableServiceAsync<IApplicationTestService>(assemblyPath, typeName, cancellationToken);

    public Task<ITestLogger> CreateTestLoggerAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
        => CreateRemotableServiceAsync<ITestLogger>(assemblyPath, typeName, cancellationToken);

    public Task<IUITestService> CreateUITestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
        => CreateRemotableServiceAsync<IUITestService>(assemblyPath, typeName, cancellationToken);

    public Task<ITestSpeechRecognitionService> CreateTestSpeechServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
        => CreateRemotableServiceAsync<ITestSpeechRecognitionService>(assemblyPath, typeName, cancellationToken);

    public Task RegisterServiceAsync(string serviceTypeName, string implementationTypeName, string assemblyPath, CancellationToken cancellationToken)
    {
        if (_coordinator == null)
        {
            throw new InvalidOperationException("Test coordinator not available. Service registration is only supported in test mode.");
        }

        _coordinator.RegisterService(serviceTypeName, implementationTypeName, assemblyPath);
        return Task.CompletedTask;
    }

    public Task ContinueStartupAsync(CancellationToken cancellationToken)
    {
        if (_coordinator == null)
        {
            throw new InvalidOperationException("Test coordinator not available. Startup continuation is only supported in test mode.");
        }

        _coordinator.ContinueStartup();
        return Task.CompletedTask;
    }

    private async Task<ServiceType> CreateRemotableServiceAsync<ServiceType>(string assemblyPath, string typeName, CancellationToken cancellationToken)
        where ServiceType : class
    {
        if (_scopeProvider == null)
        {
            throw new InvalidOperationException("Cannot create test services: IApplicationScopeProvider not available. This should not happen in normal operation.");
        }

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
