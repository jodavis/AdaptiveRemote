using AdaptiveRemote.EndtoEndTests;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class ConversationSteps : StepsBase
{
    [When(@"I say {string}")]
    public void WhenISay(string phrase)
    {
        Assert.IsNotNull(Host, "Cannot speak phrase '{0}'. The application is not started.", phrase);
        Logger.LogInformation("Simulating speech: {Phrase}", phrase);
        Host.Speech.SpeakPhrase(phrase);
    }

    [Then(@"I should see the speaking message {string} is visible")]
    public void ThenIShouldSeeTheSpeakingMessageIsVisible(string expectedMessage)
    {
        Assert.IsNotNull(Host, "Cannot check speaking message. The application is not started.");

        // Wait a bit for the UI to update and speech processing
        string? actualMessage = null;
        for (int i = 0; i < 50; i++) // Try for up to 5 seconds
        {
            actualMessage = Host.UI.GetSpeakingMessage();
            if (actualMessage == expectedMessage)
            {
                Logger.LogInformation("Speaking message verified: {Message}", actualMessage);
                return;
            }
            Thread.Sleep(100);
        }

        Assert.Fail($"Expected speaking message '{expectedMessage}' but got '{actualMessage}'");
    }
}
