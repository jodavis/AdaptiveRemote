using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Conversation;

internal class MockLogger<LoggerType> : ILogger<LoggerType>
{
    private readonly List<string> _messages = new();
    private Exception? _assertException = null;

    public IEnumerable<string> Messages => _messages;

    IDisposable? ILogger.BeginScope<TState>(TState state) => throw new NotImplementedException();
    bool ILogger.IsEnabled(LogLevel logLevel) => throw new NotImplementedException();
    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (exception is AssertFailedException ||
            exception is AssertInconclusiveException ||
            exception is Moq.MockException)
        {
            _assertException = _assertException ?? exception;
            return;
        }

        _messages.Add(formatter(state, exception));
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

            Assert.AreEqual($"\n{expectedIter.Current}", $"\n{actualIter.Current}", "MockLogger.Messages[{0}]", count);

            count++;
        }

        if (actualIter.MoveNext())
        {
            List<string> unexpectedMessages = GetRemaining(actualIter, ref count);
            Assert.AreEqual(expected.Length, count,
                "Wront number of messages. Did not expect to find:\n{0}",
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
}
