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
        Assert.IsNotNull(Host, "Cannot click the '{0}' button. The application is not started.", buttonLabel);
        Host.UI.ClickButton(buttonLabel);
    }

    [Then(@"I should see the {string} button is enabled")]
    public void ThenIShouldSeeTheButtonIsEnabled(string buttonLabel)
    {
        Assert.IsNotNull(Host, "Cannot check the state of the '{0}' button. The application is not started.", buttonLabel);
        Assert.IsTrue(Host.UI.IsButtonEnabled(buttonLabel), "Button {0} was not enabled", buttonLabel);
    }

    [When(@"I click on the text {string}")]
    public void WhenIClickOnTheText(string text)
    {
        Assert.IsNotNull(Host, "Cannot click the text '{0}'. The application is not started.", text);
        Host.UI.ClickText(text);
    }

    [Then(@"I should see the text {string} is visible")]
    public void ThenIShouldSeeTheTextIsVisible(string text)
    {
        Assert.IsNotNull(Host, "Cannot check if text '{0}' is visible. The application is not started.", text);
        Assert.IsTrue(Host.UI.IsTextVisible(text), "Text '{0}' was not visible", text);
    }

    [Then(@"I should see the text {string} is not visible")]
    public void ThenIShouldSeeTheTextIsNotVisible(string text)
    {
        Assert.IsNotNull(Host, "Cannot check if text '{0}' is not visible. The application is not started.", text);
        Assert.IsFalse(Host.UI.IsTextVisible(text), "Text '{0}' was visible but should not be", text);
    }

}
