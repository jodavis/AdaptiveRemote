using AdaptiveRemote.EndtoEndTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class UISteps : StepsBase
{
    [When(@"I click on the {string} button")]
    public void WhenIClickOnTheButton(string buttonLabel)
    {
        Host.UI.ClickButton(buttonLabel);
    }

    [Then(@"I should see the {string} button is enabled")]
    public void ThenIShouldSeeTheButtonIsEnabled(string buttonLabel)
    {
        Assert.IsTrue(Host.UI.IsButtonEnabled(buttonLabel), "Button {0} was not enabled", buttonLabel);
    }

    [When(@"I click on the text {string}")]
    public void WhenIClickOnTheText(string text)
    {
        Host.UI.ClickText(text);
    }

    [Then(@"I should see a modal message containing {string}")]
    public void ThenIShouldSeeAModalMessageContaining(string expectedText)
    {
        Host.UI.WaitForModalMessageContaining(expectedText);
    }

}
