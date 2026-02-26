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

        AdaptiveRemoteHostSettings settings = new(
            UIService: UIServiceType.Playwright,
            ExePath: exePath,
            CommandLineArgs: $"--playwright:TracesDir=\"{tracesDir}\"",
            EnvironmentVariables: System.Collections.Immutable.ImmutableDictionary<string, string>.Empty
                .Add("ASPNETCORE_ENVIRONMENT", "Development"));
        objectContainer.RegisterInstanceAs(settings);
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

    [Given(@"the host application does not use an embedded WebView2 control")]
    public static void GivenTheHostApplicationDoesNotUseAnEmbeddedWebView2Control()
    {
        // This test does not use an embedded WebView2 control, so we can simply return without doing anything.
    }
}
