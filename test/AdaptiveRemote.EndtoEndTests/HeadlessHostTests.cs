using System.Collections.Immutable;
using AdaptiveRemote.EndtoEndTests.Host;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests;

[TestClass]
public class HeadlessHostTests : HostTestBase
{
    private static string? _solutionRoot;

    private static readonly ImmutableDictionary<string, string> StandardHeadlessEnvironmentVariables =
        ImmutableDictionary<string, string>.Empty;

    public TestContext TestContext { get; set; } = null!;

    public string? TracesPath => Path.Combine(TestContext.TestResultsDirectory!, "playwright-traces");

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        _solutionRoot = GetSolutionRoot();

        context.WriteLine($"Solution root: {_solutionRoot}");
    }

    protected override AdaptiveRemoteHostSettings GetHostSettings(string solutionRoot)
    {
        // On Linux, the executable doesn't have .exe extension
        string exeName = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
            ? "AdaptiveRemote.Headless.exe"
            : "AdaptiveRemote.Headless";
        
        return new(
            ExePath: Path.Combine(solutionRoot, $"src/AdaptiveRemote.Headless/bin/Debug/net8.0/{exeName}"),
            CommandLineArgs: $"--playwright:TracesDir=\"{TracesPath}\"",
            EnvironmentVariables: StandardHeadlessEnvironmentVariables);
    }

    protected override ILogger CreateTypedLogger(AdaptiveRemoteHost host) 
        => host.CreateLogger<HeadlessHostTests>();

    [TestMethod]
    [Timeout(180000)] // 3 minutes
    public void HeadlessHost_StartsAndRespondsToTestCommands()
    {
        RunStandardE2ETestAsync(_solutionRoot!, TestContext);

        foreach (string traceFile in Directory.GetFiles(TracesPath!, "*.zip"))
        {
            TestContext.AddResultFile(traceFile);
        }
    }
}
