using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.TestUtilities;

internal class MockLogger<LoggerType1, LoggerType2> : MockLogger<LoggerType1>, ILogger<LoggerType2>
{
}

internal class MockLogger<LoggerType> : ILogger<LoggerType>
{
    private readonly List<string> _messages = new();
    private readonly object _lock = new();
    private Exception? _assertException = null;

    public IEnumerable<string> Messages => _messages;
    public TestContext? OutputWriter { get; set; }

    public List<(string find, string replace)> ReplaceStrings = new();

    IDisposable? ILogger.BeginScope<TState>(TState state) => throw new NotImplementedException();
    bool ILogger.IsEnabled(LogLevel logLevel) => true;
    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (exception is AssertFailedException ||
            exception is AssertInconclusiveException ||
            exception is Moq.MockException)
        {
            _assertException = _assertException ?? exception;
            return;
        }

        string message = $"{logLevel}[{eventId.Id}]: {formatter(state, exception)}";
        foreach ((string find, string replace) in ReplaceStrings)
        {
            message = message.Replace(find, replace);
        }
        lock (_lock)
        {
            _messages.Add(message);
        }
        OutputWriter?.WriteLine(message);
    }

    public void VerifyMessages(Action<MessageLogger> expected)
    {
        MockLogger<LoggerType> expectedLog = new();
        expectedLog.ReplaceStrings.AddRange(ReplaceStrings);
        MessageLogger messageLogger = new(expectedLog);
        expected(messageLogger);

        VerifyMessages(expectedLog._messages.ToArray());
    }

    public void VerifyMessages(params string[] expected)
    {
        // Retry a few times, in case messages are still being logged on a background thread
        for (int i = 0; i < 10; i++)
        {
            if (_assertException is not null)
            {
                throw _assertException;
            }

            if (_messages.Count >= expected.Length)
            {
                break;
            }

            Thread.Sleep(i * 5);
        }

        IEnumerator<string> expectedIter = expected.AsEnumerable().GetEnumerator();
        List<string>.Enumerator actualIter = _messages.GetEnumerator();

        int count = 0;

        while (expectedIter.MoveNext())
        {
            if (!actualIter.MoveNext())
            {
                int expectedCount = count;
                List<string> missingMessages = GetRemaining(expectedIter, ref expectedCount);
                Assert.AreEqual(expectedCount, count, "Wrong number of messages. Did not find:\n{0}",
                    string.Join("\n", missingMessages));
            }

            if (!actualIter.Current.StartsWith(expectedIter.Current))
            {
                Assert.AreEqual($"\n{expectedIter.Current}", $"\n{actualIter.Current}", "MockLogger.Messages[{0}]", count);
            }

            count++;
        }

        if (actualIter.MoveNext())
        {
            List<string> unexpectedMessages = GetRemaining(actualIter, ref count);
            Assert.AreEqual(expected.Length, count,
                "Wrong number of messages. Did not expect to find:\n{0}",
                string.Join("\n", unexpectedMessages));
        }
    }

    private static List<string> GetRemaining(IEnumerator<string> iter, ref int count)
    {
        List<string> remaining = new();

        do
        {
            remaining.Add($"[{count}]: {iter.Current}");
            count++;
        } while (iter.MoveNext());

        return remaining;
    }

    internal Task WaitForMessageAsync(Action<MessageLogger> expected)
        => WaitForMessageAsync(expected, TimeSpan.FromSeconds(5));
    internal Task WaitForMessageAsync(Action<MessageLogger> expected, TimeSpan timeout)
    {
        MockLogger<LoggerType> expectedLog = new();
        expectedLog.ReplaceStrings.AddRange(ReplaceStrings);
        MessageLogger messageLogger = new(expectedLog);
        expected(messageLogger);
        Assert.AreEqual(1, expectedLog.Messages.Count(), "Expected exactly one message to wait for");
        return WaitForMessageAsync(expectedLog.Messages.First(), timeout);
    }

    internal Task WaitForMessageAsync(string expectedMessage)
        => WaitForMessageAsync(expectedMessage, TimeSpan.FromSeconds(5));
    internal async Task WaitForMessageAsync(string expectedMessage, TimeSpan timeout)
    {
        DateTime startTime = DateTime.Now;

        bool found = false;
        while (!found)
        {
            if (_assertException is not null)
            {
                throw _assertException;
            }

            List<string> messages;
            lock (_lock)
            {
                messages = _messages.ToList(); // Make a copy
            }
            foreach (string message in messages)
            {
                if (message.StartsWith(expectedMessage))
                {
                    found = true;
                    break;
                }
            }

            await Task.Delay(100);

            Assert.IsTrue(DateTime.Now - startTime < timeout, "Timed out waiting for log message '{0}'", expectedMessage);
        }
    }

    internal void ClearMessages()
    {
        _messages.Clear();
    }
}
