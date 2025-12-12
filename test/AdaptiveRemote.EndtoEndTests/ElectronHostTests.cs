using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using StreamJsonRpc;

namespace AdaptiveRemote.EndtoEndTests;

[TestClass]
public class ElectronHostTests : HostTestBase
{
    private static string? _solutionRoot;
    private static string? _testServicesPath;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        _solutionRoot = GetSolutionRoot();
        _testServicesPath = Path.Combine(AppContext.BaseDirectory, "AdaptiveRemote.EndtoEndTests.TestServices.dll");

        context.WriteLine($"Solution root: {_solutionRoot}");
        context.WriteLine($"Test services path: {_testServicesPath}");

        if (!File.Exists(_testServicesPath))
        {
            throw new FileNotFoundException($"Test services not found at: {_testServicesPath}");
        }
    }

    protected override string GetHostPath(string solutionRoot)
    {
        // Determine runtime identifier for Electron
        string rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
        return Path.Combine(solutionRoot, $"src/AdaptiveRemote.Electron/bin/Debug/net8.0/{rid}/AdaptiveRemote.Electron.dll");
    }

    protected override void ConfigureProcessStartInfo(ProcessStartInfo startInfo)
    {
        // Set environment to prevent Electron from opening a window
        startInfo.Environment["ELECTRON_ENABLE_LOGGING"] = "1";
        startInfo.Environment["DISPLAY"] = ":99"; // Use virtual display on Linux
        startInfo.Environment["ELECTRON_DISABLE_SANDBOX"] = "1"; // Disable sandbox for CI environments
    }

    protected override void VerifyLogs(HostTestContext context)
    {
        // Don't verify logs for Electron as it may have expected warnings from Electron itself
    }

    [TestMethod]
    [Timeout(180000)] // 3 minutes
    public async Task ElectronHost_StartsAndRespondsToTestCommands()
    {
        await RunStandardE2ETestAsync(_solutionRoot!, _testServicesPath!, TestContext);
    }
}

