using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;

namespace AdaptiveRemote.Services.TestControl;

/// <summary>
/// Service that provides a test control endpoint for E2E testing.
/// Enabled via --test:ControlPort=&lt;port&gt; command-line argument.
/// </summary>
internal class TestControlService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<TestControlService> _logger;
    private TcpListener? _listener;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check if test control port is configured
        var portString = _configuration["test:ControlPort"];
        if (string.IsNullOrEmpty(portString) || !int.TryParse(portString, out int port))
        {
            // Test control endpoint not enabled
            return;
        }

        _logger.LogInformation("Starting test control endpoint on port {Port}", port);

        try
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();

            _logger.LogInformation("Test control endpoint listening on port {Port}", port);

            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                _ = HandleClientAsync(client, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test control endpoint error");
        }
        finally
        {
            _listener?.Stop();
            _logger.LogInformation("Test control endpoint stopped");
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken stoppingToken)
    {
        try
        {
            await using var stream = client.GetStream();
            var rpc = JsonRpc.Attach(stream, this);

            _logger.LogInformation("Test control client connected");

            await rpc.Completion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling test control client");
        }
        finally
        {
            client.Dispose();
        }
    }

    /// <summary>
    /// Loads a test service from the specified assembly and type.
    /// Called via JSON-RPC from the test client.
    /// </summary>
    public Task LoadTestService(string assemblyPath, string typeName)
    {
        _logger.LogInformation("Loading test service: {TypeName} from {AssemblyPath}", typeName, assemblyPath);

        try
        {
            // Validate that the assembly path is an absolute path to prevent directory traversal
            if (!Path.IsPathFullyQualified(assemblyPath))
            {
                throw new InvalidOperationException($"Assembly path must be fully qualified: {assemblyPath}");
            }

            // Validate that the file exists
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException($"Assembly not found: {assemblyPath}");
            }

            var assembly = Assembly.LoadFrom(assemblyPath);
            var type = assembly.GetType(typeName);

            if (type == null)
            {
                throw new InvalidOperationException($"Type {typeName} not found in assembly {assemblyPath}");
            }

            // Create instance with IHostApplicationLifetime if constructor accepts it
            var constructor = type.GetConstructor(new[] { typeof(IHostApplicationLifetime) });
            if (constructor != null)
            {
                _testService = Activator.CreateInstance(type, _lifetime);
            }
            else
            {
                _testService = Activator.CreateInstance(type);
            }

            _logger.LogInformation("Test service loaded successfully: {TypeName}", typeName);

            // Initialize if the service has an InitializeAsync method
            var initMethod = type.GetMethod("InitializeAsync");
            if (initMethod != null && _testService != null)
            {
                var task = (Task?)initMethod.Invoke(_testService, null);
                return task ?? Task.CompletedTask;
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load test service");
            throw;
        }
    }

    /// <summary>
    /// Gets the name of the loaded test service.
    /// Called via JSON-RPC from the test client.
    /// </summary>
    public string GetServiceName()
    {
        if (_testService == null)
        {
            return "No test service loaded";
        }

        var property = _testService.GetType().GetProperty("ServiceName");
        return property?.GetValue(_testService)?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Performs a health check on the test service.
    /// Called via JSON-RPC from the test client.
    /// </summary>
    public async Task<bool> HealthCheckAsync()
    {
        if (_testService == null)
        {
            return false;
        }

        var method = _testService.GetType().GetMethod("HealthCheckAsync");
        if (method != null)
        {
            var task = (Task<bool>?)method.Invoke(_testService, null);
            return task != null && await task;
        }

        return true;
    }

    /// <summary>
    /// Requests the application to shut down.
    /// Called via JSON-RPC from the test client.
    /// </summary>
    public async Task RequestShutdownAsync()
    {
        _logger.LogInformation("Test service requesting application shutdown");

        if (_testService != null)
        {
            var method = _testService.GetType().GetMethod("RequestShutdownAsync");
            if (method != null)
            {
                var task = (Task?)method.Invoke(_testService, null);
                if (task != null)
                {
                    await task;
                }
            }
        }

        // Give a moment for the test service to complete
        await Task.Delay(100);

        // Request shutdown via the host lifetime
        _lifetime.StopApplication();
    }
}
