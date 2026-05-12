using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.TestUtilities;

// Adds App-specific VerifyMessages overload that takes an Action<MessageLogger>,
// where MessageLogger is the App's internal source-generated logger wrapper.
// This overload is only usable from App.Tests because MessageLogger is internal to AdaptiveRemote.App.
internal static class MockLoggerAppExtensions
{
    public static void VerifyMessages<T>(this MockLogger<T> mockLogger, Action<MessageLogger> expected)
    {
        MockLogger<T> expectedLog = new();
        expectedLog.ReplaceStrings.AddRange(mockLogger.ReplaceStrings);
        MessageLogger messageLogger = new(expectedLog);
        expected(messageLogger);

        mockLogger.VerifyMessages(expectedLog.Messages.ToArray());
    }

    public static Task WaitForMessageAsync<T>(this MockLogger<T> mockLogger, Action<MessageLogger> expected)
        => mockLogger.WaitForMessageAsync(expected, TimeSpan.FromSeconds(5));

    public static Task WaitForMessageAsync<T>(this MockLogger<T> mockLogger, Action<MessageLogger> expected, TimeSpan timeout)
    {
        MockLogger<T> expectedLog = new();
        expectedLog.ReplaceStrings.AddRange(mockLogger.ReplaceStrings);
        MessageLogger messageLogger = new(expectedLog);
        expected(messageLogger);
        Assert.AreEqual(1, expectedLog.Messages.Count(), "Expected exactly one message to wait for");
        return mockLogger.WaitForMessageAsync(expectedLog.Messages.First(), timeout);
    }
}
