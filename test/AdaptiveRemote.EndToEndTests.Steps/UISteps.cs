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
        Assert.IsTrue(Host.UI.WaitForButtonEnabled(buttonLabel, enabled: true), "Button {0} was not enabled", buttonLabel);
    }

    [Then(@"I should see the {string} button is disabled")]
    public void ThenIShouldSeeTheButtonIsDisabled(string buttonLabel)
    {
        Assert.IsTrue(Host.UI.WaitForButtonEnabled(buttonLabel, enabled: false), "Button {0} was not disabled", buttonLabel);
    }

    [Then(@"I should see the {string} button is programmed")]
    public void ThenIShouldSeeTheButtonIsProgrammed(string buttonLabel)
    {
        Assert.IsTrue(Host.UI.WaitForButtonProgrammed(buttonLabel, programmed: true), "Button '{0}' was not programmed", buttonLabel);
    }

    [Then(@"I should see the {string} button is not programmed")]
    public void ThenIShouldSeeTheButtonIsNotProgrammed(string buttonLabel)
    {
        Assert.IsTrue(Host.UI.WaitForButtonProgrammed(buttonLabel, programmed: false), "Button '{0}' was unexpectedly programmed", buttonLabel);
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

    [Then(@"I should see a modal message containing")]
    public void ThenIShouldSeeAModalMessageContainingMarkup(string expectedMarkup)
    {
        Host.UI.WaitForModalMessageMarkupContaining(expectedMarkup);
    }

    [Then(@"I should not see a modal message")]
    public void ThenIShouldNotSeeAModalMessage()
    {
        Host.UI.WaitForNoModalMessage();
    }

    [Then(@"the stylesheet selector {string} should define {string} as {string}")]
    public void ThenTheStylesheetSelectorShouldDefineAs(string selector, string propertyName, string expectedValue)
    {
        string? actualValue = Host.UI.GetStylesheetRulePropertyValue(selector, propertyName);
        Assert.AreEqual(
            expectedValue,
            actualValue,
            "Expected selector '{0}' to define '{1}' as '{2}', but was '{3}'.",
            selector,
            propertyName,
            expectedValue,
            actualValue ?? "<null>");
    }
}
