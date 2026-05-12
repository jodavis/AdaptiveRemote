using AdaptiveRemote.EndtoEndTests;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class CloudLayoutSteps : StepsBase
{
    private static readonly string PrimaryLayoutPath = Path.Combine(
        Path.GetDirectoryName(typeof(CloudLayoutSteps).Assembly.Location)!,
        "Layout", "primary-layout.json");

    private static readonly string UpdatedLayoutPath = Path.Combine(
        Path.GetDirectoryName(typeof(CloudLayoutSteps).Assembly.Location)!,
        "Layout", "updated-layout.json");

    private string CachePath => Environment.CloudCachePath
        ?? throw new InvalidOperationException("CloudCachePath is not configured.");

    private string StubFilePath => Environment.CloudStubFilePath
        ?? throw new InvalidOperationException("CloudStubFilePath is not configured.");

    [Given(@"the local layout cache is empty")]
    public void GivenTheLocalLayoutCacheIsEmpty()
    {
        if (Directory.Exists(CachePath))
        {
            foreach (string file in Directory.GetFiles(CachePath))
            {
                File.Delete(file);
            }
        }
    }

    [Given(@"the local layout cache contains the primary layout")]
    public void GivenTheLocalLayoutCacheContainsThePrimaryLayout()
    {
        WriteToCacheFile(PrimaryLayoutPath);
    }

    [Given(@"the local layout cache contains the updated layout")]
    public void GivenTheLocalLayoutCacheContainsTheUpdatedLayout()
    {
        WriteToCacheFile(UpdatedLayoutPath);
    }

    [Given(@"the compiled layout service serves the primary layout")]
    public void GivenTheCompiledLayoutServiceServesThePrimaryLayout()
    {
        File.Copy(PrimaryLayoutPath, StubFilePath, overwrite: true);
    }

    [Given(@"the compiled layout service serves the updated layout")]
    public void GivenTheCompiledLayoutServiceServesTheUpdatedLayout()
    {
        File.Copy(UpdatedLayoutPath, StubFilePath, overwrite: true);
    }

    [Given(@"the compiled layout service is unavailable")]
    public void GivenTheCompiledLayoutServiceIsUnavailable()
    {
        if (File.Exists(StubFilePath))
        {
            File.Delete(StubFilePath);
        }
    }

    [Given(@"the idle cooldown is (\d+) seconds?")]
    public void GivenTheIdleCooldownIsSeconds(int seconds)
    {
        Environment.SetIdleCooldownSeconds(seconds);
    }

    [Given(@"cloud auth is configured with valid credentials")]
    public void GivenCloudAuthIsConfiguredWithValidCredentials()
    {
        Environment.SetCloudAuthCredentials(
            Environment.JwtAuthority.ValidClientId,
            Environment.JwtAuthority.ValidClientSecret);
    }

    [Given(@"cloud auth is configured with invalid credentials")]
    public void GivenCloudAuthIsConfiguredWithInvalidCredentials()
    {
        Environment.SetCloudAuthCredentials(
            Environment.JwtAuthority.ValidClientId,
            "invalid-client-secret");
    }

    [Given(@"cloud auth is configured without credentials")]
    public void GivenCloudAuthIsConfiguredWithoutCredentials()
    {
        Environment.SetCloudAuthCredentials(clientId: null, clientSecret: null);
    }

    [When(@"the compiled layout service is updated to the updated layout")]
    public void WhenTheCompiledLayoutServiceIsUpdatedToTheUpdatedLayout()
    {
        File.Copy(UpdatedLayoutPath, StubFilePath, overwrite: true);
    }

    [Then(@"I should not see the Guide button")]
    public void ThenIShouldNotSeeTheGuideButton()
    {
        bool exists = Host.UI.WaitForButtonExists("Guide", TimeSpan.FromSeconds(2));
        Assert.IsFalse(exists, "Guide button should not be visible in the primary layout.");
    }

    [Then(@"I should see a fatal startup error message")]
    public void ThenIShouldSeeAFatalStartupErrorMessage()
    {
        Host.Application.WaitForPhase(LifecyclePhase.FatalError, timeout: TimeSpan.FromSeconds(60));
    }

    // Stop the host after cloud-layout scenarios so fatal-error or incomplete states don't leak
    // into subsequent tests that reuse the running host.
    [AfterScenario("cloud-layout")]
    public void AfterScenario_StopHostAfterCloudLayoutTest()
    {
        Environment.StopHostIfRunning();
    }

    private void WriteToCacheFile(string sourceFixturePath)
    {
        Directory.CreateDirectory(CachePath);
        string cacheFile = Path.Combine(CachePath, "layout.cache");
        File.Copy(sourceFixturePath, cacheFile, overwrite: true);
    }
}
