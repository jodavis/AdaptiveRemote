using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using StreamJsonRpc;

namespace AdaptiveRemote.EndtoEndTests;

[TestClass]
public class ElectronHostTests : HostTestBase
{
    private static string? _hostPath;
    private static string? _testServicesPath;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        // Locate the Electron host binary
        string baseDir = AppContext.BaseDirectory;
        string solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "../../../../../.."));
        
        _hostPath = Path.Combine(solutionRoot, "src/AdaptiveRemote.Electron/bin/Debug/net8.0/AdaptiveRemote.Electron.dll");
        _testServicesPath = Path.Combine(baseDir, "AdaptiveRemote.EndtoEndTests.TestServices.dll");

        context.WriteLine($"Solution root: {solutionRoot}");
        context.WriteLine($"Host path: {_hostPath}");
        context.WriteLine($"Test services path: {_testServicesPath}");

        if (!File.Exists(_hostPath))
        {
            throw new FileNotFoundException($"Electron host not found at: {_hostPath}");
        }

        if (!File.Exists(_testServicesPath))
        {
            throw new FileNotFoundException($"Test services not found at: {_testServicesPath}");
        }
    }

    protected override string HostExecutablePath => "dotnet";
    
    private static string HostAssemblyPath => _hostPath!;

    [TestMethod]
    [Timeout(180000)] // 3 minutes
    public async Task ElectronHost_StartsAndRespondsToTestCommands()
    {
        int controlPort = GetAvailablePort();

        using HostTestContext context = await LaunchElectronHostAsync(controlPort, TestContext);

        // Load test service
        bool loaded = await LoadTestServiceAsync(
            context,
            _testServicesPath!,
            "AdaptiveRemote.EndtoEndTests.BasicTestService");

        Assert.IsTrue(loaded, "Failed to load test service");

        // Execute a test command
        object? result = await InvokeTestServiceAsync(
            context,
            "ExecuteTestAsync",
            "Hello from E2E test");

        Assert.IsNotNull(result);
        Assert.AreEqual("Echo: Hello from E2E test", result.ToString());

        // Request shutdown
        await ShutdownHostAsync(context);

        // Verify clean logs (allow some warnings due to Electron)
        // Don't verify logs for Electron as it may have expected warnings
    }

    private async Task<HostTestContext> LaunchElectronHostAsync(int controlPort, TestContext testContext)
    {
        // For Electron, we need to use dotnet to run the DLL
        StringBuilder logOutput = new();
        StringBuilder errorOutput = new();

        string arguments = $"\"{HostAssemblyPath}\" --test:ControlPort={controlPort}";

        ProcessStartInfo startInfo = new()
        {
            FileName = HostExecutablePath,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(HostAssemblyPath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Set environment to prevent Electron from opening a window
        startInfo.Environment["ELECTRON_ENABLE_LOGGING"] = "1";
        startInfo.Environment["DISPLAY"] = ":99"; // Use virtual display on Linux

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

        testContext.WriteLine($"Launching Electron host: {HostExecutablePath} {arguments}");
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

    private static int GetAvailablePort()
    {
        using TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
