using System.Runtime.InteropServices;
using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.EndToEndTests.TestServices;
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

        AudioDetectionHelper.AssertHasAudioInputAndOutput();
    }

    [BeforeTestRun]
    public static void ConfigureHostSettings(IObjectContainer objectContainer)
    {
        string deploymentPath = Path.GetDirectoryName(typeof(WpfHostTestHooks).Assembly.Location)!;

        objectContainer.RegisterInstanceAs(new AdaptiveRemoteHostSettings(
            UIService: UIServiceType.BlazorWebView,
            ExePath: Path.Combine(deploymentPath, "AdaptiveRemote.exe")));
    }
}
