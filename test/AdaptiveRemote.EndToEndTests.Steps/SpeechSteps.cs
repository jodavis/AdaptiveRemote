using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class SpeechSteps : StepsBase
{
    private ITestSpeechRecognitionEngine? _testSpeechEngine;

    [Given(@"the application is running with test speech recognition")]
    public async Task GivenTheApplicationIsRunningWithTestSpeechRecognitionAsync()
    {
        // The host should start with the test speech recognition engine injected
        // For now, we'll start the application normally
        // In the future, this will inject the TestSpeechRecognitionEngine before host build

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
        // For now, we'll skip this test until the service injection is fully implemented
        Assert.Inconclusive(
            "Test speech recognition engine injection is not yet fully implemented. " +
            "See ADR-149 for implementation details.");

        await Task.CompletedTask;
    }

    [When(@"I say ""(.*)""")]
    public async Task WhenISayAsync(string phrase)
    {
        Assert.IsNotNull(_testSpeechEngine, "Test speech engine is not available");
        await _testSpeechEngine.SpeakAsync(phrase);

        // Give the application time to process the speech input
        await Task.Delay(500);
    }

    [Then(@"the application should enter listening mode")]
    public void ThenTheApplicationShouldEnterListeningMode()
    {
        // TODO: Check that the application is in listening mode
        // This would check the ConversationView or UI state
        Assert.Inconclusive("Listening mode verification not yet implemented");
    }

    [Then(@"the application should exit listening mode")]
    public void ThenTheApplicationShouldExitListeningMode()
    {
        // TODO: Check that the application has exited listening mode
        // This would check the ConversationView or UI state
        Assert.Inconclusive("Listening mode verification not yet implemented");
    }
}
