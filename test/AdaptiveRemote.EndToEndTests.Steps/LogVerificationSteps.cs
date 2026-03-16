using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class LogVerificationSteps : StepsBase
{
    private static readonly Dictionary<string, int> _lastLineRead = new();

    [Then("I should not see any warning or error messages in the logs")]
    public void ThenIShouldNotSeeAnyWarningsOrErrorsInTheLogFile()
    {
        IEnumerable<string> warningAndErrorLines = FilterLogLines(IsWarningOrError);

        Assert.IsFalse(
            warningAndErrorLines.Any(),
            "Host log contains warnings or errors:\n{0}",
            string.Join("\n", warningAndErrorLines));
    }

    [Then("I should not see any error messages in the logs")]
    public void ThenIShouldNotSeeAnyErrorsInTheLogFile()
    {
        IEnumerable<string> errorLines = FilterLogLines(IsError);

        Assert.IsFalse(
            errorLines.Any(),
            "Host log contains errors:\n{0}",
            string.Join("\n", errorLines));
    }

    [Then("I should see an error message in the logs:")]
    public void ThenIShouldSeeAnErrorInTheLogs(string expectedErrorMessage)
    {
        IEnumerable<string> errorLines = FilterLogLines(IsError);

        Assert.IsTrue(errorLines.Any(), "Host log does not contain any error messages.");
        Assert.AreEqual(1, errorLines.Count(),
            "Host log contains unexpected errors:\n{0}",
            string.Join("\n", errorLines));
        StringAssert.Contains(errorLines.First(), expectedErrorMessage,
            "Host log error message does not match the expected text");
    }

    [AfterScenario]
    public void OnAfterScenario_CheckLogsForWarningsOrErrors()
    {
        ThenIShouldNotSeeAnyErrorsInTheLogFile();
    }

    private IEnumerable<string> FilterLogLines(Func<string, bool> lineFilter)
    {
        Assert.IsNotNull(Environment.HostLogs, "Host log path was not set.");

        if (!File.Exists(Environment.HostLogs))
        {
            Logger.LogWarning("Host log file does not exist at expected location: {LogPath}", Environment.HostLogs);
        }

        string logContent;
        using (Stream logStream = File.Open(Environment.HostLogs, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            logContent = new StreamReader(logStream).ReadToEnd();
        }

        string[] logLines = logContent.Split(System.Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        return FilterLines(logLines, lineFilter);
    }

    private IEnumerable<string> FilterLines(string[] logLines, Func<string, bool> lineFilter)
    {
        Assert.IsNotNull(Environment.HostLogs, "Host log path was not set.");

        IEnumerable<string> filteredLines = logLines;
        if (_lastLineRead.TryGetValue(Environment.HostLogs, out int lastLine))
        {
            filteredLines = logLines.Skip(lastLine);
        }
        _lastLineRead[Environment.HostLogs] = logLines.Length;

        return filteredLines.Where(lineFilter);
    }

    private static bool IsError(string line)
    {
        return line.Contains("] Error [", StringComparison.Ordinal);
    }

    private static bool IsWarningOrError(string line)
    {
        return line.Contains("] Error [", StringComparison.Ordinal)
            || line.Contains("] Warning [", StringComparison.Ordinal);
    }
}
