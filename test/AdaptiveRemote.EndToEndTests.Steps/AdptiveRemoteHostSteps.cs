using AdaptiveRemote.EndtoEndTests;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class AdptiveRemoteHostSteps : StepsBase
{
    [Given(@"the application is running")]
    public void GivenTheApplicationIsRunning()
    {
        Environment.EnsureHostStarted();
    }

    [Given(@"the application is not running")]
    public void GivenTheApplicationIsNotRunning()
    {
        Environment.StopHostIfRunning();
    }

    [Given(@"the application is in the {LifecyclePhase} phase")]
    public void GivenTheApplicationIsInAnExpectedLifecyclePhase(LifecyclePhase expected)
    {
        GivenTheApplicationIsRunning();

        // Wait for the application to be in the specified state
        ThenIShouldSeeTheApplicationInAnExpectedLifecyclePhase(expected);
    }

    [When(@"I start the application")]
    public void WhenIStartTheApplication()
    {
        Environment.StartHost();
    }

    [When(@"I wait for the application to shut down")]
    public void WhenIWaitForTheApplicationToShutDown()
    {
        Host.WaitForShutdown();
    }

    [Then(@"I should see the application in the {LifecyclePhase} phase")]
    public void ThenIShouldSeeTheApplicationInAnExpectedLifecyclePhase(LifecyclePhase expected)
    {
        Host.Application.WaitForPhase(expected, timeout: TimeSpan.FromSeconds(60));
    }
}
