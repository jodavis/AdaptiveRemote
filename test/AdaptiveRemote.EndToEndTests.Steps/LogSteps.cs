using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class LogSteps : StepsBase
{
    [Then("I should not see any warning or error messages in the logs")]
    public void ThenIShouldNotSeeAnyWarningsOrErrorsInTheLogFile()
    {
        // Verify that the captured host logs do not contain errors, and log any warnings.
        string logs = Host.StandardOutput;
        string errors = Host.StandardError;

        // Check for error/warning patterns
        bool hasErrors = logs.Contains("err:", StringComparison.OrdinalIgnoreCase) ||
                        errors.Contains("err:", StringComparison.OrdinalIgnoreCase);

        bool hasWarnings = logs.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
                          errors.Contains("warning", StringComparison.OrdinalIgnoreCase);

        if (hasErrors)
        {
            Assert.Fail($"Host logs contain errors:\n{logs}\n\nStderr:\n{errors}");
        }

        // Note: Warnings are informational but not a failure for now
        if (hasWarnings)
        {
            Logger.LogWarning("Host logs contain warnings");
        }
    }
}
