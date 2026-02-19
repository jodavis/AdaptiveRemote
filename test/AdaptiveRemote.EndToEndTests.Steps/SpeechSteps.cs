using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.Services.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class SpeechSteps : StepsBase
{
    private ITestSpeechRecognitionEngine? _testSpeechEngine;

    [When("I say {string}")]
    public void WhenISay(string phrase)
    {
        // Get the test speech engine on first use
        if (_testSpeechEngine is null)
        {
            _testSpeechEngine = WaitHelpers.WaitForAsyncTask(
                ct => Host.TestEndpoint.GetTestServiceProviderAsync(ct)
                    .ContinueWith(t => t.Result.GetTestSpeechEngineAsync(ct), ct)
                    .Unwrap(),
                timeout: TimeSpan.FromSeconds(10));

            Assert.IsNotNull(_testSpeechEngine, "Test speech recognition engine was not injected into the host");
        }

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
