using System.Runtime.InteropServices;

namespace AdaptiveRemote.EndtoEndTests;

[TestClass]
public class WpfHostTests : HostTestBase
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

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            context.WriteLine("Skipping WPF host test - requires Windows");
            return;
        }

        if (!File.Exists(_testServicesPath))
        {
            throw new FileNotFoundException($"Test services not found at: {_testServicesPath}");
        }
    }

    protected override string GetHostPath(string solutionRoot)
    {
        return Path.Combine(solutionRoot, "src/AdaptiveRemote/bin/Debug/net8.0-windows/AdaptiveRemote.exe");
    }

    protected override string GetHostExecutable()
    {
        // WPF host is an .exe, run it directly (not via dotnet)
        return GetHostPath(_solutionRoot!);
    }

    [TestMethod]
    [Timeout(180000)] // 3 minutes
    public async Task WpfHost_StartsAndRespondsToTestCommands()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Inconclusive("WPF host test requires Windows");
            return;
        }

        string hostPath = GetHostPath(_solutionRoot!);
        if (!File.Exists(hostPath))
        {
            Assert.Inconclusive($"WPF host not built at: {hostPath}");
            return;
        }

        await RunStandardE2ETestAsync(_solutionRoot!, _testServicesPath!, TestContext);
    }
}
