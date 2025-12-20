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
internal class TestControlService : BackgroundService, ITestControlService
{
    private readonly TestingSettings _settings;
    private readonly IApplicationScopeProvider _scopeProvider;
    private readonly ILogger<TestControlService> _logger;
    private TcpListener? _listener;
    private Type? _testServiceType;

    public TestControlService(
        IOptions<TestingSettings> settings,
        IApplicationScopeProvider scopeProvider,
        ILogger<TestControlService> logger)
    {
        _settings = settings.Value;
        _scopeProvider = scopeProvider;
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

    private async Task HandleClientAsync(TcpClient client, CancellationToken stoppingToken)
    {
        try
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                JsonRpc rpc = JsonRpc.Attach(stream, this);

                stoppingToken.Register(rpc.Dispose);

                _logger.LogInformation("Test control client connected");

                await rpc.Completion;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling test control client");
        }
    }

    public async Task<ITestService> CreateTestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading test service: {TypeName} from {AssemblyPath}", typeName, assemblyPath);

        Assembly assembly = Assembly.LoadFrom(assemblyPath);
        
        Type? serviceType = assembly.GetType(typeName)
            ?? throw new ArgumentException($"Type not found: {typeName}", nameof(typeName));

        // Store the type to instantiate later within each scoped invocation
        _testServiceType = serviceType;
        _logger.LogInformation("Test service type loaded successfully");
        
        // Create the test service within the application scope so it gets access to scoped services
        ITestService? testService = null;
        await _scopeProvider.InvokeInScopeAsync(async (scopedProvider, ct) =>
        {
            testService = (ITestService)ActivatorUtilities.CreateInstance(scopedProvider, serviceType);
            await Task.CompletedTask;
        }, cancellationToken);
        
        return testService!;
    }
}
