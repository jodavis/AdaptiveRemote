using AdaptiveRemote.EndtoEndTests.Host;
using Reqnroll;
using Reqnroll.BoDi;

namespace AdaptiveRemote.Backend.ApiTests.Hooks;

[Binding]
public static class ApiTestHooks
{
    [BeforeTestRun]
    public static void ConfigureHostSettings(IObjectContainer objectContainer)
    {
        // AdaptiveRemoteHost is not configured for this test project
        objectContainer.RegisterInstanceAs(new AdaptiveRemoteHostSettings(
            UIService: UIServiceType.BlazorWebView,
            ExePath: string.Empty));
    }
}
