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
internal class TestControlService : BackgroundService
{
    private readonly TestingSettings _settings;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TestControlService> _logger;
    private TcpListener? _listener;
    private object? _testService;

    public TestControlService(
        IOptions<TestingSettings> settings,
        IServiceProvider serviceProvider,
        ILogger<TestControlService> logger)
    {
        _settings = settings.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings.ControlPort is null)
        {
            // Test control endpoint not requested
            return;
        }

        _logger.LogInformation("Starting test control endpoint on port {Port}", _settings.ControlPort.Value);

        _listener = new TcpListener(IPAddress.Loopback, _settings.ControlPort.Value);
        _listener.Start();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(CancellationToken.None);
                    _ = HandleClientAsync(client, stoppingToken);
                }
                catch (ObjectDisposedException)
                {
                    // Listener was stopped
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting test control connection");
                }
            }
        }
        finally
        {
            _logger.LogInformation("Stopping test control endpoint");
            _listener.Stop();
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

            // Create instance using service provider for dependency injection
            _testService = ActivatorUtilities.CreateInstance(_serviceProvider, serviceType);

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
