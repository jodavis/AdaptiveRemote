using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace AdaptiveRemote.EndtoEndTests;

[TestClass]
public class ConsoleHostTests : HostTestBase
{
    private static string? _hostPath;
    private static string? _testServicesPath;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        // Locate the Console host binary
        string baseDir = AppContext.BaseDirectory;
        string solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "../../../../.."));
        
        _hostPath = Path.Combine(solutionRoot, "src/AdaptiveRemote.Console/bin/Debug/net8.0-windows7.0/AdaptiveRemote.Console.exe");
        _testServicesPath = Path.Combine(baseDir, "AdaptiveRemote.EndtoEndTests.TestServices.dll");

        context.WriteLine($"Solution root: {solutionRoot}");
        context.WriteLine($"Host path: {_hostPath}");
        context.WriteLine($"Test services path: {_testServicesPath}");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            context.WriteLine("Skipping Console host test - requires Windows");
            return;
        }

        if (!File.Exists(_testServicesPath))
        {
            throw new FileNotFoundException($"Test services not found at: {_testServicesPath}");
        }
    }

    protected override string HostExecutablePath => _hostPath ?? "";

    [TestMethod]
    [Timeout(180000)] // 3 minutes
    public async Task ConsoleHost_StartsAndRespondsToTestCommands()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Inconclusive("Console host test requires Windows");
            return;
        }

        if (!File.Exists(_hostPath))
        {
            Assert.Inconclusive($"Console host not built at: {_hostPath}");
            return;
        }

        int controlPort = GetAvailablePort();

        using HostTestContext context = await LaunchHostAsync(controlPort, TestContext);

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
            "Hello from Console E2E test");

        Assert.IsNotNull(result);
        Assert.AreEqual("Echo: Hello from Console E2E test", result.ToString());

        // Request shutdown
        await ShutdownHostAsync(context);

        // Verify logs
        VerifyLogs(context);
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
