using System.Runtime.InteropServices;
using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.EndToEndTests.TestServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;
using Reqnroll.BoDi;

namespace AdaptiveRemote.EndToEndTests.ConsoleHost.Hooks;

[Binding]
public static class ConsoleHostTestHooks
{
    [BeforeScenario]
    public static void SkipTestsOnNonWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Inconclusive("Console host test requires Windows");
        }

        AudioDetectionHelper.AssertHasAudioInputAndOutput();
    }

    [BeforeTestRun]
    public static void ConfigureHostSettings(IObjectContainer objectContainer)
    {
        string deploymentPath = Path.GetDirectoryName(typeof(ConsoleHostTestHooks).Assembly.Location)!;

        objectContainer.RegisterInstanceAs(new AdaptiveRemoteHostSettings(
            UIService: UIServiceType.BlazorWebView,
            ExePath: Path.Combine(deploymentPath, "AdaptiveRemote.Console.exe")));
    }

    [Given(@"the host application does not use an embedded WebView2 control")]
    public static void GivenTheHostApplicationDoesNotUseAnEmbeddedWebView2Control()
    {
        Assert.Inconclusive("Skipping this test because the console host uses a hosted WebView2 control.");
    }
}
