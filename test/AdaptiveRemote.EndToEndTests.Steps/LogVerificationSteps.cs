using AdaptiveRemote.TestUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class LogVerificationSteps : StepsBase
{
    private const string HostName = "Host";
    private const string RawLayoutServiceName = "RawLayoutService";
    private const string CompiledLayoutServiceName = "CompiledLayoutService";
    private const string LayoutProcessingServiceName = "LayoutProcessingService";
    private const string ServiceFilter = "(" + RawLayoutServiceName + "|" + CompiledLayoutServiceName + "|" + LayoutProcessingServiceName + ")";

    private static readonly Dictionary<string, int> _lastLineRead = new();

    [Then("I should not see any warning or error messages in the logs")]
    public void ThenIShouldNotSeeAnyWarningsOrErrorsInTheLogFile()
    {
        ThenIShouldNotSeeAnyWarningsOrErrorsInTheServiceLogs(HostName);
    }

    [Then("^I should not see any warning or error messages in the " + ServiceFilter + " logs")]
    public void ThenIShouldNotSeeAnyWarningsOrErrorsInTheServiceLogs(string serviceName)
    {
        IEnumerable<string> warningAndErrorLines = FilterLogLines(GetLogFileFor(serviceName), IsWarningOrError);

        Assert.IsFalse(
            warningAndErrorLines.Any(),
            "{0} log contains warnings or errors:\n{1}",
            serviceName,
            string.Join("\n", warningAndErrorLines));
    }

    [Then("I should not see any error messages in the logs")]
    public void ThenIShouldNotSeeAnyErrorsInTheLogFile()
    {
        ThenIShouldNotSeeAnyErrorsInTheServiceLogs(HostName);
    }

    [Then("^I should not see any error messages in the " + ServiceFilter + " logs")]
    public void ThenIShouldNotSeeAnyErrorsInTheServiceLogs(string serviceName)
    {
        IEnumerable<string> errorLines = FilterLogLines(GetLogFileFor(serviceName), IsError);

        Assert.IsFalse(
            errorLines.Any(),
            "{0} log contains errors:\n{1}",
            serviceName,
            string.Join("\n", errorLines));
    }

    [Then("I should see an error message in the logs:")]
    public void ThenIShouldSeeAnErrorInTheLogs(string expectedErrorMessage)
    {
        ThenIShouldSeeAnErrorInTheServiceLogs(HostName, expectedErrorMessage);
    }

    [Then("^I should see an error message in the " + ServiceFilter + " logs:")]
    public void ThenIShouldSeeAnErrorInTheServiceLogs(string serviceName, string expectedErrorMessage)
    {
        IEnumerable<string>? errorLines = null;
        string logFilePath = GetLogFileFor(serviceName);

        WaitHelpers.ExecuteWithRetries(() =>
        {
            errorLines = FilterLogLines(logFilePath, IsError);
            return errorLines.Any(line => line.Contains(expectedErrorMessage, StringComparison.Ordinal));
        });

        Assert.IsNotNull(errorLines, "Failed to read {0} log lines.", serviceName);
        Assert.IsTrue(errorLines.Any(), "{0} log does not contain any error messages.", serviceName);
        Assert.AreEqual(1, errorLines.Count(),
            "{0} log contains unexpected errors:\n{1}",
            serviceName,
            string.Join("\n", errorLines));
        StringAssert.Contains(errorLines.First(), expectedErrorMessage,
            "{0} log error message does not match the expected text", serviceName);
    }

    [Then("I should see a warning message in the logs:")]
    public void ThenIShouldSeeAWarningInTheLogs(string expectedWarningMessage)
    {
        ThenIShouldSeeAWarningInTheServiceLogs(HostName, expectedWarningMessage);
    }

    [Then("^I should see a warning message in the " + ServiceFilter + " logs:")]
    public void ThenIShouldSeeAWarningInTheServiceLogs(string serviceName, string expectedWarningMessage)
    {
        IEnumerable<string>? warningAndErrorLines = null;
        string logFilePath = GetLogFileFor(serviceName);

        WaitHelpers.ExecuteWithRetries(() =>
        {
            warningAndErrorLines = FilterLogLines(logFilePath, IsWarningOrError);
            return warningAndErrorLines.Any(line => line.Contains(expectedWarningMessage, StringComparison.Ordinal));
        });

        Assert.IsNotNull(warningAndErrorLines, "Failed to read {0} log lines.", serviceName);
        Assert.IsTrue(warningAndErrorLines.Any(), "{0} log does not contain any error messages.", serviceName);
        Assert.AreEqual(1, warningAndErrorLines.Count(),
            "{0} log contains unexpected errors:\n{1}",
            serviceName,
            string.Join("\n", warningAndErrorLines));
        StringAssert.Contains(warningAndErrorLines.First(), expectedWarningMessage,
            "{0} log warning message does not match the expected text", serviceName);
    }

    [Then("^I should see a message that contains \"(.*)\" in the logs")]
    public void ThenIShouldSeeAMessageThatContainsSomethingInTheLogs(string expectedMessagePart)
    {
        ThenIShouldSeeAMessageThatContainsSomethingInTheServiceLogs(expectedMessagePart, HostName);
    }

    [Then("^I should see a message that contains \"(.*)\" in the " + ServiceFilter + " logs")]
    public void ThenIShouldSeeAMessageThatContainsSomethingInTheServiceLogs(string expectedMessagePart, string serviceName)
    {
        string logFilePath = GetLogFileFor(serviceName);

        bool result = WaitHelpers.ExecuteWithRetries(() =>
        {
            foreach (string line in EnumerateLogLines(logFilePath))
            {
                if (IsWarningOrError(line))
                {
                    Assert.Fail("Found an error or warning in the {0} log while looking for a message containing '{1}':\n{2}",
                        serviceName,
                        expectedMessagePart,
                        line);
                }

                if (line.Contains(expectedMessagePart, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        });

        Assert.IsTrue(result, "Did not find a message in the {0} log containing '{1}'", serviceName);
    }

    private string GetLogFileFor(string serviceName)
    {
        string? logPath = serviceName switch
        {
            HostName => Environment.HostLogs,
            RawLayoutServiceName => Environment.RawLayoutServiceLogs,
            CompiledLayoutServiceName => Environment.CompiledLayoutServiceLogs,
            LayoutProcessingServiceName => Environment.LayoutProcessingServiceLogs,
            _ => throw new ArgumentException($"Unexpected service name: {serviceName}", nameof(serviceName))
        };

        Assert.IsNotNull(logPath, $"{serviceName} log path was not set.");
        if (!File.Exists(logPath))
        {
            Logger.LogWarning("{ServiceName} log file does not exist at expected location: {LogPath}", serviceName, logPath);
        }

        return logPath;
    }

    private static IEnumerable<string> EnumerateLogLines(string logFilePath)
    {
        int currentLine = 0;

        _lastLineRead.TryGetValue(logFilePath, out int lastLineRead);

        using (Stream logStream = File.Open(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (StreamReader logReader = new(logStream))
        {
            string? logLine;
            while ((logLine = logReader.ReadLine()) is not null)
            {
                currentLine++;
                if (currentLine > lastLineRead)
                {
                    _lastLineRead[logFilePath] = currentLine;
                    yield return logLine;
                }
            }
        }
    }

    private static string[] FilterLogLines(string logFilePath, Func<string, bool> lineFilter)
    {
        return EnumerateLogLines(logFilePath)
            .Where(lineFilter)
            .ToArray();
    }

    private static bool IsError(string line)
    {
        return line.Contains("] Error [", StringComparison.Ordinal)
            || line.Contains("] [Error] [", StringComparison.Ordinal);
    }

    private static bool IsWarning(string line)
    {
        return line.Contains("] Warning [", StringComparison.Ordinal)
            || line.Contains("] [Warning] [", StringComparison.Ordinal);
    }

    private static bool IsWarningOrError(string line)
    {
        return IsError(line) || IsWarning(line);
    }
}
