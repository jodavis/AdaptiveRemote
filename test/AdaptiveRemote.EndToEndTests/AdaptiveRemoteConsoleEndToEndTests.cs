using System.IO;

namespace AdaptiveRemote.EndToEndTests;

/// <summary>
/// End-to-end tests for the AdaptiveRemote.Console host application.
/// </summary>
[TestClass]
public class AdaptiveRemoteConsoleEndToEndTests : HostEndToEndTestBase
{
    protected override string GetHostExecutablePath()
    {
        // The executable should be in the build output directory
        string testAssemblyPath = typeof(AdaptiveRemoteConsoleEndToEndTests).Assembly.Location;
        string testDirectory = Path.GetDirectoryName(testAssemblyPath)!;

        // Go up to the solution root and find the AdaptiveRemote.Console executable
        string solutionRoot = Path.GetFullPath(Path.Combine(testDirectory, "..", "..", "..", "..", ".."));
        string hostPath = Path.Combine(solutionRoot, "src", "AdaptiveRemote.Console", "bin", "Debug", "net8.0-windows7.0", "AdaptiveRemote.Console.exe");

        return hostPath;
    }

    protected override string GetReadyLogMessage()
    {
        // The message that indicates the application has completed initialization
        return "Ready";
    }

    [TestMethod]
    [TestCategory("EndToEnd")]
    [TestCategory("RequiresWindows")]
    public async Task AdaptiveRemoteConsole_LaunchesAndRespondsToTestControl()
    {
        await RunEndToEndTestAsync();
    }
}
