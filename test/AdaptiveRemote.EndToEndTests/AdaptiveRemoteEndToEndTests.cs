using System.IO;

namespace AdaptiveRemote.EndToEndTests;

/// <summary>
/// End-to-end tests for the AdaptiveRemote WPF host application.
/// </summary>
[TestClass]
public class AdaptiveRemoteEndToEndTests : HostEndToEndTestBase
{
    protected override string GetHostExecutablePath()
    {
        // The executable should be in the build output directory
        // Navigate from test output to the AdaptiveRemote output directory
        string testAssemblyPath = typeof(AdaptiveRemoteEndToEndTests).Assembly.Location;
        string testDirectory = Path.GetDirectoryName(testAssemblyPath)!;

        // Go up to the solution root and find the AdaptiveRemote executable
        // Assuming structure: test/AdaptiveRemote.EndToEndTests/bin/Debug/net8.0/
        // Target: src/AdaptiveRemote/bin/Debug/net8.0-windows/AdaptiveRemote.exe
        string solutionRoot = Path.GetFullPath(Path.Combine(testDirectory, "..", "..", "..", "..", ".."));
        string hostPath = Path.Combine(solutionRoot, "src", "AdaptiveRemote", "bin", "Debug", "net8.0-windows", "AdaptiveRemote.exe");

        return hostPath;
    }

    protected override string GetReadyLogMessage()
    {
        // The message that indicates the application has completed initialization
        // Based on ApplicationLifecycle setting phase to Ready
        return "Ready";
    }

    [TestMethod]
    [TestCategory("EndToEnd")]
    [TestCategory("RequiresWindows")]
    public async Task AdaptiveRemote_LaunchesAndRespondsToTestControl()
    {
        await RunEndToEndTestAsync();
    }
}
