using System.Net.Sockets;
using StreamJsonRpc;
using AdaptiveRemote.EndToEndTests.TestServices;

namespace AdaptiveRemote.EndToEndTests.Infrastructure;

/// <summary>
/// Client for connecting to the test control endpoint via TCP JSON-RPC.
/// </summary>
public class TestControlClient : IDisposable
{
    private TcpClient? _tcpClient;
    private JsonRpc? _rpc;
    private bool _disposed;

    public bool IsConnected => _tcpClient?.Connected ?? false;

    /// <summary>
    /// Connects to the test control endpoint.
    /// </summary>
    /// <param name="port">The TCP port to connect to.</param>
    /// <param name="timeout">Connection timeout.</param>
    public async Task ConnectAsync(int port, TimeSpan timeout)
    {
        _tcpClient = new TcpClient();

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await _tcpClient.ConnectAsync("127.0.0.1", port, cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Failed to connect to test control endpoint on port {port} within {timeout.TotalSeconds}s");
        }

        var stream = _tcpClient.GetStream();
        _rpc = JsonRpc.Attach(stream);
    }

    /// <summary>
    /// Loads a test service into the host application.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly containing the test service.</param>
    /// <param name="typeName">Full type name of the test service.</param>
    public async Task LoadTestServiceAsync(string assemblyPath, string typeName)
    {
        if (_rpc == null)
            throw new InvalidOperationException("Not connected to test control endpoint");

        await _rpc.InvokeAsync("LoadTestService", assemblyPath, typeName);
    }

    /// <summary>
    /// Gets a proxy to the loaded test service.
    /// </summary>
    public ITestService GetTestServiceProxy()
    {
        if (_rpc == null)
            throw new InvalidOperationException("Not connected to test control endpoint");

        // Note: This creates a proxy for invoking methods on the remote test service
        // The actual test service instance is managed by the TestControlService
        // We use the existing JsonRpc connection to invoke methods
        throw new NotSupportedException(
            "GetTestServiceProxy is not currently implemented. " +
            "Use InvokeAsync to call methods directly on the test service.");
    }

    /// <summary>
    /// Invokes a method on the test control endpoint.
    /// </summary>
    public async Task<T> InvokeAsync<T>(string methodName, params object[] args)
    {
        if (_rpc == null)
            throw new InvalidOperationException("Not connected to test control endpoint");

        return await _rpc.InvokeAsync<T>(methodName, args);
    }

    /// <summary>
    /// Invokes a method on the test control endpoint.
    /// </summary>
    public async Task InvokeAsync(string methodName, params object[] args)
    {
        if (_rpc == null)
            throw new InvalidOperationException("Not connected to test control endpoint");

        await _rpc.InvokeAsync(methodName, args);
    }

    /// <summary>
    /// Requests the application to shut down via the test service.
    /// </summary>
    public async Task RequestShutdownAsync()
    {
        await InvokeAsync("RequestShutdownAsync");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _rpc?.Dispose();
            _tcpClient?.Dispose();
            _disposed = true;
        }
    }
}
