using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.Services.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Application;

[Binding]
public class AccessibilitySteps : StepsBase
{
    private IReadOnlyList<AccessibilityViolation>? _accessibilityViolations;

    [When(@"I run the accessibility contrast checker")]
    public void WhenIRunTheAccessibilityContrastChecker()
    {
        // Run the accessibility checker and store results
        _accessibilityViolations = Host.UI.CheckAccessibility();
    }

    [Then(@"I should not see any accessibility contrast violations")]
    public void ThenIShouldNotSeeAnyAccessibilityContrastViolations()
    {
        Assert.IsNotNull(
            _accessibilityViolations,
            "Accessibility checker has not been run.");

        // Filter for only color-contrast violations
        List<AccessibilityViolation> contrastViolations = _accessibilityViolations
            .Where(v => v.RuleId.Contains(
                "color-contrast",
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (contrastViolations.Count > 0)
        {
            // Build a detailed error message
            string errorMessage =
                $"Found {contrastViolations.Count} color contrast violation(s):\n\n";

            foreach (AccessibilityViolation violation in contrastViolations)
            {
                errorMessage += $"Rule: {violation.RuleId}\n";
                errorMessage += $"Impact: {violation.Impact}\n";
                errorMessage += $"Description: {violation.Description}\n";
                errorMessage += $"Help: {violation.HelpText}\n";
                errorMessage += $"Help URL: {violation.HelpUrl}\n";
                errorMessage += $"Element: {violation.HtmlSnippet}\n\n";
            }

            Assert.Fail(errorMessage);
        }
    }
}
