using StreamJsonRpc;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Base class for E2E tests that launch and control host applications.
/// </summary>
public abstract class HostTestBase
{
    protected abstract string HostExecutablePath { get; }
    protected virtual string? HostWorkingDirectory => null;
    protected virtual TimeSpan StartupTimeout => TimeSpan.FromSeconds(120);
    protected virtual TimeSpan RpcTimeout => TimeSpan.FromSeconds(30);
    protected virtual TimeSpan ShutdownTimeout => TimeSpan.FromSeconds(30);

    protected async Task<HostTestContext> LaunchHostAsync(int controlPort, TestContext testContext)
    {
        StringBuilder logOutput = new();
        StringBuilder errorOutput = new();

        string arguments = $"--test:ControlPort={controlPort}";

        ProcessStartInfo startInfo = new()
        {
            FileName = HostExecutablePath,
            Arguments = arguments,
            WorkingDirectory = HostWorkingDirectory ?? Path.GetDirectoryName(HostExecutablePath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process process = new()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                logOutput.AppendLine(e.Data);
                testContext.WriteLine($"[OUT] {e.Data}");
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                errorOutput.AppendLine(e.Data);
                testContext.WriteLine($"[ERR] {e.Data}");
            }
        };

        testContext.WriteLine($"Launching host: {HostExecutablePath} {arguments}");
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for the host to be ready and establish control connection
        JsonRpc? rpc = null;
        TcpClient? client = null;
        Exception? connectionError = null;

        CancellationTokenSource cts = new(StartupTimeout);

        try
        {
            // Retry connection attempts for up to StartupTimeout
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    client = new TcpClient();
                    await client.ConnectAsync("127.0.0.1", controlPort, cts.Token);
                    rpc = JsonRpc.Attach(client.GetStream());
                    testContext.WriteLine("Connected to test control endpoint");
                    break;
                }
                catch (SocketException ex)
                {
                    connectionError = ex;
                    client?.Dispose();
                    client = null;
                    await Task.Delay(500, cts.Token);
                }
            }

            if (rpc is null)
            {
                throw new TimeoutException(
                    $"Failed to connect to test control endpoint on port {controlPort} within {StartupTimeout}. " +
                    $"Last error: {connectionError?.Message}");
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"Failed to connect to test control endpoint on port {controlPort} within {StartupTimeout}");
        }

        return new HostTestContext(process, rpc, client!, logOutput, errorOutput, testContext);
    }

    protected async Task<bool> LoadTestServiceAsync(
        HostTestContext context,
        string assemblyPath,
        string typeName)
    {
        using CancellationTokenSource cts = new(RpcTimeout);

        try
        {
            bool result = await context.Rpc
                .InvokeWithCancellationAsync<bool>(
                    "LoadTestServiceAsync",
                    new object[] { assemblyPath, typeName },
                    cts.Token);

            return result;
        }
        catch (Exception ex)
        {
            context.TestContext.WriteLine($"Failed to load test service: {ex.Message}");
            throw;
        }
    }

    protected async Task<object?> InvokeTestServiceAsync(
        HostTestContext context,
        string methodName,
        params object?[] args)
    {
        using CancellationTokenSource cts = new(RpcTimeout);

        try
        {
            object? result = await context.Rpc
                .InvokeWithCancellationAsync<object?>(
                    "InvokeTestServiceAsync",
                    new object?[] { methodName, args },
                    cts.Token);

            return result;
        }
        catch (Exception ex)
        {
            context.TestContext.WriteLine($"Failed to invoke test service method: {ex.Message}");
            throw;
        }
    }

    protected async Task ShutdownHostAsync(HostTestContext context)
    {
        context.TestContext.WriteLine("Requesting host shutdown...");

        try
        {
            await InvokeTestServiceAsync(context, "RequestShutdownAsync");
        }
        catch
        {
            // Shutdown request may fail if host is already shutting down
        }

        using CancellationTokenSource cts = new(ShutdownTimeout);

        try
        {
            await context.Process.WaitForExitAsync(cts.Token);
            context.TestContext.WriteLine($"Host exited with code: {context.Process.ExitCode}");
        }
        catch (OperationCanceledException)
        {
            context.TestContext.WriteLine("Host did not exit within timeout, killing process");
            try
            {
                context.Process.Kill(entireProcessTree: true);
                // Give the kill signal time to take effect
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                context.TestContext.WriteLine($"Error killing process: {ex.Message}");
            }
            // For Electron tests, don't fail if shutdown times out but process was killed successfully
            if (!context.Process.HasExited)
            {
                throw new TimeoutException($"Host did not exit within {ShutdownTimeout}");
            }
        }
    }

    protected static void VerifyLogs(HostTestContext context)
    {
        string logs = context.LogOutput.ToString();
        string errors = context.ErrorOutput.ToString();

        // Check for error/warning patterns
        bool hasErrors = logs.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                        errors.Contains("error", StringComparison.OrdinalIgnoreCase);

        bool hasWarnings = logs.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
                          errors.Contains("warning", StringComparison.OrdinalIgnoreCase);

        if (hasErrors)
        {
            Assert.Fail($"Host logs contain errors:\n{logs}\n\nStderr:\n{errors}");
        }

        // Note: Warnings are informational but not a failure for now
        if (hasWarnings)
        {
            context.TestContext.WriteLine("WARNING: Host logs contain warnings");
        }
    }

    protected static int GetAvailablePort()
    {
        using TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

public class HostTestContext : IDisposable
{
    public Process Process { get; }
    public JsonRpc Rpc { get; }
    public TcpClient Client { get; }
    public StringBuilder LogOutput { get; }
    public StringBuilder ErrorOutput { get; }
    public TestContext TestContext { get; }

    public HostTestContext(
        Process process,
        JsonRpc rpc,
        TcpClient client,
        StringBuilder logOutput,
        StringBuilder errorOutput,
        TestContext testContext)
    {
        Process = process;
        Rpc = rpc;
        Client = client;
        LogOutput = logOutput;
        ErrorOutput = errorOutput;
        TestContext = testContext;
    }

    public void Dispose()
    {
        try
        {
            Rpc?.Dispose();
        }
        catch { }

        try
        {
            Client?.Dispose();
        }
        catch { }

        try
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
            }
            Process.Dispose();
        }
        catch { }

        GC.SuppressFinalize(this);
    }
}
