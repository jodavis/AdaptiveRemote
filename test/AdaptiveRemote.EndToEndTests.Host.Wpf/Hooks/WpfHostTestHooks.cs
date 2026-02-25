using System.Runtime.InteropServices;
using AdaptiveRemote.EndtoEndTests.Host;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;
using Reqnroll.BoDi;

namespace AdaptiveRemote.EndToEndTests.WpfHost.Hooks;

[Binding]
public static class WpfHostTestHooks
{
    [BeforeScenario]
    public static void SkipTestsOnNonWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Inconclusive("WPF host test requires Windows");
        }
    }

    [BeforeTestRun]
    public static void ConfigureHostSettings(IObjectContainer objectContainer)
    {
        string deploymentPath = Path.GetDirectoryName(typeof(WpfHostTestHooks).Assembly.Location)!;

        objectContainer.RegisterInstanceAs(new AdaptiveRemoteHostSettings(
            UIService: UIServiceType.BlazorWebView,
            ExePath: Path.Combine(deploymentPath, "AdaptiveRemote.exe")));
    }

    [Given(@"the host application does not use an embedded WebView2 control")]
    public static void GivenTheHostApplicationDoesNotUseAnEmbeddedWebView2Control()
    {
        Assert.Inconclusive("Skipping this test because the WPF host uses a hosted WebView2 control.");
    }
}
