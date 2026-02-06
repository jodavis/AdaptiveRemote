using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class LogVerificationSteps : StepsBase
{
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
    public void ThenIShouldNotSeeAnErrorsInTheLogFile()
    {
        IEnumerable<string> errorLines = FilterLogLines(IsError);

        Assert.IsFalse(
            errorLines.Any(),
            "Host log contains errors:\n{0}",
            string.Join("\n", errorLines));
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

        return logLines.Where(lineFilter);
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
