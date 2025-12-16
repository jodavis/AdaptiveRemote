using System.Collections.Immutable;
using AdaptiveRemote.EndtoEndTests.Host;

namespace AdaptiveRemote.EndtoEndTests;

[TestClass]
public class ElectronHostTests : HostTestBase
{
    private static string? _solutionRoot;

    private static readonly ImmutableDictionary<string, string> StandardElectronEnvironmentVariables =
        ImmutableDictionary<string, string>.Empty
            .Add("ELECTRON_ENABLE_LOGGING", "1")
            .Add("DISPLAY", ":99") // Use virtual display on Linux
            .Add("ELECTRON_DISABLE_SANDBOX", "1") // Disable sandbox for CI environments
            .Add("ELECTRON_DISABLE_GPU", "1"); // Disable GPU for headless environments

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        _solutionRoot = GetSolutionRoot();

        context.WriteLine($"Solution root: {_solutionRoot}");
    }

    protected override AdaptiveRemoteHostSettings GetHostSettings(string solutionRoot)
    {
        string rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
        // On Linux, the executable doesn't have .exe extension
        string exeName = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
            ? "AdaptiveRemote.Electron.exe"
            : "AdaptiveRemote.Electron";
        
        // Add Electron-specific flags for headless operation
        string electronArgs = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)
            ? "--no-sandbox --disable-gpu --disable-dev-shm-usage"
            : "";
        
        return new(
            ExePath: Path.Combine(solutionRoot, $"src/AdaptiveRemote.Electron/bin/Debug/net8.0/{rid}/{exeName}"),
            CommandLineArgs: electronArgs,
            EnvironmentVariables: StandardElectronEnvironmentVariables);
    }

    [TestMethod]
    [Timeout(180000)] // 3 minutes
    public void ElectronHost_StartsAndRespondsToTestCommands()
    {
        RunStandardE2ETestAsync(_solutionRoot!, TestContext);
    }
}

