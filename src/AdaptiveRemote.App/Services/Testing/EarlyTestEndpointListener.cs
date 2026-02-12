using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using System.Net;
using System.Net.Sockets;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Early TCP listener for test endpoint that starts before host Build().
/// Accepts the first test connection, handles early RPC calls (RegisterService, ContinueStartup),
/// then forwards subsequent calls to TestEndpointService after Build().
/// </summary>
public class EarlyTestEndpointListener : IDisposable
{
    private readonly TestEndpointCoordinator _coordinator;
    private readonly ILogger<EarlyTestEndpointListener>? _logger;
    private readonly int _controlPort;
    private TcpListener? _listener;
    private TcpClient? _client;
    private bool _disposed;
    private ITestEndpoint? _forwardTarget;

    public EarlyTestEndpointListener(
        IConfiguration configuration,
        TestEndpointCoordinator coordinator,
        ILogger<EarlyTestEndpointListener>? logger = null)
    {
        _coordinator = coordinator;
        _logger = logger;

        int? port = configuration.GetValue<int?>("test:ControlPort");
        if (!port.HasValue)
        {
            throw new InvalidOperationException("EarlyTestEndpointListener requires test:ControlPort to be configured");
        }

        _controlPort = port.Value;
    }

    /// <summary>
    /// Starts listening for the first test connection.
    /// </summary>
    public void StartListening()
    {
        if (_listener != null)
        {
            return; // Already listening
        }

        _logger?.LogInformation("Starting early test endpoint listener on port {Port}", _controlPort);

        _listener = new TcpListener(IPAddress.Loopback, _controlPort);
        _listener.Start();
    }

    /// <summary>
    /// Waits for and accepts the first test connection, then sets up RPC with a forwarding target.
    /// Returns true if connection established, false if timeout.
    /// </summary>
    public bool WaitForConnection(TimeSpan timeout, ITestEndpoint forwardTarget)
    {
        if (_listener == null)
        {
            throw new InvalidOperationException("Must call StartListening() before WaitForConnection()");
        }

        if (_client != null)
        {
            return true; // Already connected
        }

        _forwardTarget = forwardTarget;
        _logger?.LogInformation("Waiting for test connection (timeout: {Timeout})", timeout);

        try
        {
            Task<TcpClient> acceptTask = _listener.AcceptTcpClientAsync();
            if (!acceptTask.Wait(timeout))
            {
                _logger?.LogError("Timeout waiting for test connection");
                return false;
            }

            _client = acceptTask.Result;
            _logger?.LogInformation("Test client connected");

            // Set up JSON-RPC on the connection with a forwarding wrapper
            NetworkStream stream = _client.GetStream();
            ForwardingTestEndpoint forwarder = new(_coordinator, _forwardTarget, _logger);
            JsonRpc rpc = JsonRpc.Attach(stream, forwarder);

            _logger?.LogInformation("RPC endpoint ready");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to accept test connection");
            return false;
        }
    }

    /// <summary>
    /// Stops listening for new connections.
    /// </summary>
    public void StopListening()
    {
        if (_listener != null)
        {
            _logger?.LogInformation("Stopping early test endpoint listener");
            _listener.Stop();
            _listener = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopListening();
        _client?.Dispose();
    }

    /// <summary>
    /// Forwarding wrapper that handles early calls and forwards others to TestEndpointService.
    /// </summary>
    private class ForwardingTestEndpoint : ITestEndpoint
    {
        private readonly TestEndpointCoordinator _coordinator;
        private readonly ITestEndpoint _target;
        private readonly ILogger? _logger;

        public ForwardingTestEndpoint(TestEndpointCoordinator coordinator, ITestEndpoint target, ILogger? logger)
        {
            _coordinator = coordinator;
            _target = target;
            _logger = logger;
        }

        // Early initialization methods - handle locally
        public Task RegisterServiceAsync(string serviceTypeName, string implementationTypeName, string assemblyPath, CancellationToken cancellationToken)
        {
            _coordinator.RegisterService(serviceTypeName, implementationTypeName, assemblyPath);
            return Task.CompletedTask;
        }

        public Task ContinueStartupAsync(CancellationToken cancellationToken)
        {
            _coordinator.ContinueStartup();
            return Task.CompletedTask;
        }

        // Service creation methods - forward to TestEndpointService (which has scope provider)
        public Task<IApplicationTestService> CreateTestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
            => _target.CreateTestServiceAsync(assemblyPath, typeName, cancellationToken);

        public Task<ITestLogger> CreateTestLoggerAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
            => _target.CreateTestLoggerAsync(assemblyPath, typeName, cancellationToken);

        public Task<IUITestService> CreateUITestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
            => _target.CreateUITestServiceAsync(assemblyPath, typeName, cancellationToken);

        public Task<ITestSpeechRecognitionService> CreateTestSpeechServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
            => _target.CreateTestSpeechServiceAsync(assemblyPath, typeName, cancellationToken);
    }
}
