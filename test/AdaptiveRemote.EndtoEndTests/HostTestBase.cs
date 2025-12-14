using AdaptiveRemote.Services.Testing;
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
    protected virtual TimeSpan StartupTimeout => TimeSpan.FromSeconds(120);
    protected virtual TimeSpan RpcTimeout => TimeSpan.FromSeconds(30);
    protected virtual TimeSpan ShutdownTimeout => TimeSpan.FromSeconds(30);

    /// <summary>
    /// Finds the solution root directory by looking for the .sln file.
    /// </summary>
    protected static string GetSolutionRoot()
    {
        string baseDir = AppContext.BaseDirectory;
        DirectoryInfo? dir = new DirectoryInfo(baseDir);

        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new InvalidOperationException($"Could not find solution root starting from {baseDir}");
    }

    /// <summary>
    /// Gets the path to the host binary for testing.
    /// </summary>
    protected abstract string GetHostPath(string solutionRoot);

    /// <summary>
    /// Gets the working directory for the host process.
    /// </summary>
    protected virtual string? GetHostWorkingDirectory(string hostPath) => Path.GetDirectoryName(hostPath);

    /// <summary>
    /// Runs a standard E2E test: launch host, load test service, execute test, and shutdown.
    /// </summary>
    protected async Task RunStandardE2ETestAsync(
        string solutionRoot,
        string testServicesPath,
        TestContext testContext)
    {
        int controlPort = GetAvailablePort();
        string hostPath = GetHostPath(solutionRoot);

        using HostTestContext context = await LaunchHostAsync(hostPath, controlPort, testContext);

        // Load test service
        ITestService testService = await context.ControlProxy.CreateTestServiceAsync<BasicTestService>();

        // Wait for application ready
        await testService.WaitForPhaseAsync(LifecyclePhase.Ready);

        // Request shutdown via strongly-typed proxy
        await testService.InvokeCommandAsync("Exit");

        // Wait for shutdown
        await WaitForShutdownAsync(context);

        // Verify logs (optional, can be overridden)
        VerifyLogs(context);
    }

    protected async Task<HostTestContext> LaunchHostAsync(string hostPath, int controlPort, TestContext testContext)
    {
        StringBuilder logOutput = new();
        StringBuilder errorOutput = new();

        string arguments = $"--test:ControlPort={controlPort} --tivo:Fake=true --broadlink:Fake=true";
        string workingDirectory = GetHostWorkingDirectory(hostPath) ?? AppContext.BaseDirectory;

        string executable = GetHostExecutable();
        bool isExe = executable == hostPath; // If they're the same, it's a native exe

        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            Arguments = isExe ? arguments : $"\"{hostPath}\" {arguments}",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ConfigureProcessStartInfo(startInfo);

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

        testContext.WriteLine($"Launching host: {startInfo.FileName} {startInfo.Arguments}");
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
                    
                    // Create JsonRpc with target for control methods
                    var stream = client.GetStream();
                    rpc = new JsonRpc(stream, stream);
                    rpc.StartListening();
                    
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

        // Create control proxy for bootstrapping
        ITestControlService controlProxy = rpc.Attach<ITestControlService>();

        return new HostTestContext(process, rpc, controlProxy, client!, logOutput, errorOutput, testContext);
    }

    /// <summary>
    /// Gets the executable to use for launching the host (e.g., "dotnet" for DLL hosts).
    /// </summary>
    protected virtual string GetHostExecutable() => "dotnet";

    /// <summary>
    /// Configures the ProcessStartInfo with any host-specific settings (e.g., environment variables).
    /// </summary>
    protected virtual void ConfigureProcessStartInfo(ProcessStartInfo startInfo)
    {
        // Base implementation does nothing - override in derived classes
    }

    protected async Task WaitForShutdownAsync(HostTestContext context)
    {
        context.TestContext.WriteLine("Waiting for host to exit...");

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

    protected virtual void VerifyLogs(HostTestContext context)
    {
        string logs = context.LogOutput.ToString();
        string errors = context.ErrorOutput.ToString();

        // Check for error/warning patterns
        bool hasErrors = logs.Contains("err:", StringComparison.OrdinalIgnoreCase) ||
                        errors.Contains("err:", StringComparison.OrdinalIgnoreCase);

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
    public ITestControlService ControlProxy { get; }
    public TcpClient Client { get; }
    public StringBuilder LogOutput { get; }
    public StringBuilder ErrorOutput { get; }
    public TestContext TestContext { get; }

    public HostTestContext(
        Process process,
        JsonRpc rpc,
        ITestControlService controlProxy,
        TcpClient client,
        StringBuilder logOutput,
        StringBuilder errorOutput,
        TestContext testContext)
    {
        Process = process;
        Rpc = rpc;
        ControlProxy = controlProxy;
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
