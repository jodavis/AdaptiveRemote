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
        Assert.IsNotNull(Host, "Cannot click on text '{0}'. The application is not started.", text);
        Host.UI.ClickText(text);
    }

    [Then(@"I should see the text {string}")]
    public void ThenIShouldSeeTheText(string text)
    {
        Assert.IsNotNull(Host, "Cannot check for text '{0}'. The application is not started.", text);
        Assert.IsTrue(Host.UI.IsTextVisible(text), "Text '{0}' was not visible", text);
    }

    [Then(@"I should not see the text {string}")]
    public void ThenIShouldNotSeeTheText(string text)
    {
        Assert.IsNotNull(Host, "Cannot check for text '{0}'. The application is not started.", text);
        Assert.IsFalse(Host.UI.IsTextVisible(text), "Text '{0}' was visible but should not have been", text);
    }

    [Then(@"I should see a modal with class {string} containing {string}")]
    public void ThenIShouldSeeAModalWithClassContaining(string cssClass, string textContent)
    {
        Assert.IsNotNull(Host, "Cannot check for modal with class '{0}'. The application is not started.", cssClass);
        Assert.IsTrue(Host.UI.IsElementWithClassVisible(cssClass, textContent), 
            "Modal with class '{0}' containing text '{1}' was not visible", cssClass, textContent);
    }

    [Then(@"I should not see a modal with class {string}")]
    public void ThenIShouldNotSeeAModalWithClass(string cssClass)
    {
        Assert.IsNotNull(Host, "Cannot check for modal with class '{0}'. The application is not started.", cssClass);
        Assert.IsFalse(Host.UI.IsElementWithClassVisible(cssClass, null), 
            "Modal with class '{0}' was visible but should not have been", cssClass);
    }

}
