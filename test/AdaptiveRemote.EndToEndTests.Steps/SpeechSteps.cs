using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.Services.Lifecycle;
using AdaptiveRemote.Services.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class SpeechSteps : StepsBase
{
    private ITestSpeechRecognitionEngine? _testSpeechEngine;

    [Given(@"the application is running")]
    public void GivenTheApplicationIsRunning()
    {
        // Start the application if not already running
        if (!IsHostRunning)
        {
            _ = Host; // Trigger lazy initialization
        }

        // Wait for the application to be ready
        Host.Application.WaitForPhase(
            LifecyclePhase.Ready,
            timeout: TimeSpan.FromSeconds(60));

        // Get the test speech engine from the host
        _testSpeechEngine = WaitHelpers.WaitForAsyncTask(
            ct => Host.TestEndpoint.GetTestServiceProviderAsync(ct)
                .ContinueWith(t => t.Result.GetTestSpeechEngineAsync(ct), ct)
                .Unwrap(),
            timeout: TimeSpan.FromSeconds(10));

        Assert.IsNotNull(_testSpeechEngine, "Test speech recognition engine was not injected into the host");
    }

    [When("I say {string}")]
    public void WhenISay(string phrase)
    {
        Assert.IsNotNull(_testSpeechEngine, "Test speech engine is not available");
        WaitHelpers.WaitForAsyncTask(ct => _testSpeechEngine.SpeakAsync(phrase), TimeSpan.FromSeconds(5));
    }

    [Then(@"the application should enter listening mode")]
    public void ThenTheApplicationShouldEnterListeningMode()
    {
        // Poll until the application is in listening mode
        Host.Application.WaitForIsListening(expected: true, timeoutInSeconds: 10);
    }

    [Then(@"the application should exit listening mode")]
    public void ThenTheApplicationShouldExitListeningMode()
    {
        // Poll until the application has exited listening mode
        Host.Application.WaitForIsListening(expected: false, timeoutInSeconds: 10);
    }
}
