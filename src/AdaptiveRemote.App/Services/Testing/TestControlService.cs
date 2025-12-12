using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Provides a test control endpoint via TCP/JSON-RPC for E2E testing.
/// Enabled when --test:ControlPort argument is provided.
/// </summary>
internal class TestControlService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<TestControlService> _logger;
    private TcpListener? _listener;
    private Task? _listenerTask;
    private CancellationTokenSource? _cts;
    private object? _testService;

    public TestControlService(
        IConfiguration configuration,
        IHostApplicationLifetime lifetime,
        ILogger<TestControlService> logger)
    {
        _configuration = configuration;
        _lifetime = lifetime;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        int? port = _configuration.GetValue<int?>("test:ControlPort");

        if (port is null)
        {
            // Test control endpoint not requested
            return Task.CompletedTask;
        }

        _logger.LogInformation("Starting test control endpoint on port {Port}", port.Value);

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, port.Value);
        _listener.Start();

        _listenerTask = AcceptConnectionsAsync(_cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_listener is null)
        {
            return;
        }

        _logger.LogInformation("Stopping test control endpoint");

        _cts?.Cancel();
        _listener.Stop();

        if (_listenerTask is not null)
        {
            try
            {
                await _listenerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _cts?.Dispose();
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                TcpClient client = await _listener!.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting test control connection");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                JsonRpc rpc = JsonRpc.Attach(stream, this);

                _logger.LogInformation("Test control client connected");

                await rpc.Completion;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling test control client");
        }
    }

    /// <summary>
    /// Loads a test service from the specified assembly and type.
    /// This is called via JSON-RPC from the test orchestrator.
    /// </summary>
    public async Task<bool> LoadTestServiceAsync(string assemblyPath, string typeName)
    {
        try
        {
            _logger.LogInformation("Loading test service: {TypeName} from {AssemblyPath}", typeName, assemblyPath);

            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            Type? serviceType = assembly.GetType(typeName);

            if (serviceType is null)
            {
                _logger.LogError("Test service type not found: {TypeName}", typeName);
                return false;
            }

            // Create instance with shutdown callback
            _testService = Activator.CreateInstance(serviceType, new Action(() =>
            {
                _logger.LogInformation("Test service requested shutdown");
                _lifetime.StopApplication();
            }));

            if (_testService is null)
            {
                _logger.LogError("Failed to create test service instance");
                return false;
            }

            _logger.LogInformation("Test service loaded successfully");
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load test service");
            return await Task.FromResult(false);
        }
    }

    /// <summary>
    /// Invokes a method on the loaded test service.
    /// This is called via JSON-RPC from the test orchestrator.
    /// </summary>
    public async Task<object?> InvokeTestServiceAsync(string methodName, object?[]? args)
    {
        if (_testService is null)
        {
            throw new InvalidOperationException("No test service loaded");
        }

        MethodInfo? method = _testService.GetType().GetMethod(methodName);
        if (method is null)
        {
            throw new InvalidOperationException($"Method not found: {methodName}");
        }

        object? result = method.Invoke(_testService, args);

        if (result is Task task)
        {
            await task;

            // Check if it's Task<T>
            Type resultType = task.GetType();
            if (resultType.IsGenericType)
            {
                PropertyInfo? resultProperty = resultType.GetProperty("Result");
                return resultProperty?.GetValue(task);
            }

            return null;
        }

        return result;
    }
}
