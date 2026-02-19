using AdaptiveRemote.EndtoEndTests.Host;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;
using Reqnroll.BoDi;

namespace AdaptiveRemote.EndToEndTests.HeadlessHost.Hooks;

[Binding]
public static class HeadlessHostTestHooks
{
    [BeforeTestRun]
    public static void ConfigureHostSettings(IObjectContainer objectContainer)
    {
        string deploymentPath = Path.GetDirectoryName(typeof(HeadlessHostTestHooks).Assembly.Location)!;
        string exeName = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                    ? "AdaptiveRemote.Headless.exe"
                    : "AdaptiveRemote.Headless";
        string exePath = Path.Combine(deploymentPath, exeName);
        string tracesDir = Path.Combine(deploymentPath, "playwright-traces");

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Host executable not found at path: {exePath}");
        }

        objectContainer.RegisterInstanceAs(new AdaptiveRemoteHostSettings(
            UIService: UIServiceType.Playwright,
            ExePath: exePath,
            CommandLineArgs: $"--playwright:TracesDir=\"{tracesDir}\"",
            EnvironmentVariables: System.Collections.Immutable.ImmutableDictionary<string, string>.Empty
                .Add("ASPNETCORE_ENVIRONMENT", "Development")
            ) with { ShutdownTimeout = TimeSpan.FromSeconds(120) });
    }

    [AfterScenario]
    public static void OnAfterScenario_AttachPlaywrightTraces(TestContext testContext)
    {
        string deploymentPath = Path.GetDirectoryName(typeof(HeadlessHostTestHooks).Assembly.Location)!;
        string tracesDir = Path.Combine(deploymentPath, "playwright-traces");

        foreach (string traceFile in Directory.GetFiles(tracesDir, "*.zip"))
        {
            testContext.AddResultFile(traceFile);
        }
    }
}
