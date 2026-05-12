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
        string logFilePath = GetLogFileFor(serviceName);
        LogLine[] unreadLines = ReadUnreadLogLines(logFilePath);
        string[] warningAndErrorLines = unreadLines
            .Where(line => IsWarningOrError(line.Text))
            .Select(line => line.Text)
            .ToArray();
        MarkLinesAsRead(logFilePath, unreadLines);

        Assert.IsFalse(
            warningAndErrorLines.Length > 0,
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
        string logFilePath = GetLogFileFor(serviceName);
        LogLine[] unreadLines = ReadUnreadLogLines(logFilePath);
        string[] errorLines = unreadLines
            .Where(line => IsError(line.Text))
            .Select(line => line.Text)
            .ToArray();
        MarkLinesAsRead(logFilePath, unreadLines);

        Assert.IsFalse(
            errorLines.Length > 0,
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
        string logFilePath = GetLogFileFor(serviceName);
        string[]? errorLines = null;
        LogLine? matchingError = null;

        WaitHelpers.ExecuteWithRetries(() =>
        {
            LogLine[] unreadLines = ReadUnreadLogLines(logFilePath);
            matchingError = unreadLines
                .Where(line => IsError(line.Text))
                .FirstOrDefault(line => line.Text.Contains(expectedErrorMessage, StringComparison.Ordinal));

            if (matchingError is null)
            {
                return false;
            }

            errorLines = unreadLines
                .Where(line => line.Number <= matchingError.Number && IsError(line.Text))
                .Select(line => line.Text)
                .ToArray();
            MarkLinesAsRead(logFilePath, matchingError.Number);
            return true;
        });

        Assert.IsNotNull(errorLines, "Failed to read {0} log lines.", serviceName);
        Assert.IsTrue(errorLines.Length > 0, "{0} log does not contain any error messages.", serviceName);
        Assert.AreEqual(1, errorLines.Length,
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
        string logFilePath = GetLogFileFor(serviceName);
        string[]? warningAndErrorLines = null;
        LogLine? matchingWarning = null;

        WaitHelpers.ExecuteWithRetries(() =>
        {
            LogLine[] unreadLines = ReadUnreadLogLines(logFilePath);
            matchingWarning = unreadLines
                .Where(line => IsWarningOrError(line.Text))
                .FirstOrDefault(line => line.Text.Contains(expectedWarningMessage, StringComparison.Ordinal));

            if (matchingWarning is null)
            {
                return false;
            }

            warningAndErrorLines = unreadLines
                .Where(line => line.Number <= matchingWarning.Number && IsWarningOrError(line.Text))
                .Select(line => line.Text)
                .ToArray();
            MarkLinesAsRead(logFilePath, matchingWarning.Number);
            return true;
        });

        Assert.IsNotNull(warningAndErrorLines, "Failed to read {0} log lines.", serviceName);
        Assert.IsTrue(warningAndErrorLines.Length > 0, "{0} log does not contain any error messages.", serviceName);
        Assert.AreEqual(1, warningAndErrorLines.Length,
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
            LogLine[] unreadLines = ReadUnreadLogLines(logFilePath);
            LogLine? matchingLine = unreadLines
                .FirstOrDefault(line => line.Text.Contains(expectedMessagePart, StringComparison.Ordinal));

            if (matchingLine is not null)
            {
                LogLine? warningOrErrorBeforeMatch = unreadLines
                    .FirstOrDefault(line => line.Number <= matchingLine.Number && IsWarningOrError(line.Text));

                if (warningOrErrorBeforeMatch is not null)
                {
                    Assert.Fail("Found an error or warning in the {0} log while looking for a message containing '{1}':\n{2}",
                        serviceName,
                        expectedMessagePart,
                        warningOrErrorBeforeMatch.Text);
                }

                MarkLinesAsRead(logFilePath, matchingLine.Number);
                return true;
            }

            LogLine? warningOrErrorLine = unreadLines.FirstOrDefault(line => IsWarningOrError(line.Text));
            if (warningOrErrorLine is not null)
            {
                Assert.Fail("Found an error or warning in the {0} log while looking for a message containing '{1}':\n{2}",
                    serviceName,
                    expectedMessagePart,
                    warningOrErrorLine.Text);
            }

            return false;
        });

        Assert.IsTrue(result, "Did not find a message in the {0} log containing '{1}'", serviceName, expectedMessagePart);
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

    private static LogLine[] ReadUnreadLogLines(string logFilePath)
    {
        int currentLine = 0;
        List<LogLine> unreadLines = [];

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
                    unreadLines.Add(new(currentLine, logLine));
                }
            }
        }

        return unreadLines.ToArray();
    }

    private static void MarkLinesAsRead(string logFilePath, IEnumerable<LogLine> lines)
    {
        LogLine? lastLine = lines.LastOrDefault();
        if (lastLine is not null)
        {
            MarkLinesAsRead(logFilePath, lastLine.Number);
        }
    }

    private static void MarkLinesAsRead(string logFilePath, int lastLineRead)
    {
        _lastLineRead[logFilePath] = lastLineRead;
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

    private sealed record LogLine(int Number, string Text);
}
