using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class DebugSteps : StepsBase
{
    [When("I fail for no reason")]
    public static void WhenIFailForNoReason()
    {
        Assert.Fail("Test failed for no reason");
    }

    [Then("I should log an error for no reason")]
    public void ThenIShouldLogAnErrorForNoReason()
    {
        Logger.LogError("Logging an error for no reason");
    }
}
