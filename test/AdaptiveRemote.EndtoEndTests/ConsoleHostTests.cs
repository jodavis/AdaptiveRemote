using System.Runtime.InteropServices;
using AdaptiveRemote.EndtoEndTests.Host;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests;

[TestClass]
public class ConsoleHostTests : HostTestBase
{
    private static string? _solutionRoot;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        _solutionRoot = GetSolutionRoot();

        context.WriteLine($"Solution root: {_solutionRoot}");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            context.WriteLine("Skipping Console host test - requires Windows");
            return;
        }
    }

    protected override AdaptiveRemoteHostSettings GetHostSettings(string solutionRoot) 
        => new(
            UIService: UIServiceType.BlazorWebView, 
            ExePath: Path.Combine(solutionRoot, "src/AdaptiveRemote.Console/bin/Debug/net8.0-windows7.0/AdaptiveRemote.Console.exe"));

    protected override ILogger CreateTypedLogger(AdaptiveRemoteHost host) 
        => host.CreateLogger<ConsoleHostTests>();

    [TestMethod]
    [Timeout(180000)] // 3 minutes
    public void ConsoleHost_StartsAndRespondsToTestCommands()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Inconclusive("Console host test requires Windows");
            return;
        }

        AudioDetectionHelper.AssertHasAudioInputAndOutput();

        RunStandardE2ETestAsync(_solutionRoot!, TestContext);
    }
}
