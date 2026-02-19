using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.Services.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class SpeechSteps : StepsBase
{
    private ISpeechTestService? _testSpeechEngine;

    [When("I say {string}")]
    public void WhenISay(string phrase)
    {
        Host.Speech.Speak(phrase);
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
