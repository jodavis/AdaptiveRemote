namespace AdaptiveRemote.TestUtilities;

internal static class TaskAssert
{
    public static void IsComplete(Task? task, string message, params object[] args)
        => IsComplete(task, TimeSpan.Zero, message, args);

    public static void IsComplete(Task? task, TimeSpan timeout, string message, params object[] args)
    {
        Assert.IsNotNull(task, message, args);

        WaitForComplete(task, timeout, message, args);

        Assert.IsTrue(task.IsCompleted, "Task.IsComplete. {0}", FormatMessage(message, args));
        Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.Exception?.InnerException);
        Assert.IsFalse(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
    }

    public static void IsComplete<T>(ValueTask<T> task, string message, params object[] args)
        => IsComplete(task, TimeSpan.Zero, message, args);

    public static void IsComplete<T>(ValueTask<T> task, TimeSpan timeout, string message, params object[] args)
        => IsComplete(task.AsTask(), timeout, message, args);

    public static void ResultEquals<T>(Task<T> task, T expected, string message, params object[] args)
        => ResultEquals(task, expected, TimeSpan.Zero, message, args);

    public static void ResultEquals<T>(Task<T> task, T expected, TimeSpan timeout, string message, params object[] args)
    {
        IsComplete(task, timeout, message, args);

        Assert.AreEqual(expected, task.Result, "Task.Result. {0}", FormatMessage(message, args));
    }

    public static void ResultEquals<T>(ValueTask<T> task, T expected, string message, params object[] args)
        => ResultEquals(task, expected, TimeSpan.Zero, message, args);

    public static void ResultEquals<T>(ValueTask<T> task, T expected, TimeSpan timeout, string message, params object[] args)
        => ResultEquals(task.AsTask(), expected, timeout, message, args);

    public static void IsNotComplete(Task? task, string message, params object[] args)
        => IsNotComplete(task, TimeSpan.Zero, message, args);

    public static void IsNotComplete(Task? task, TimeSpan timeout, string message, params object[] args)
    {
        Assert.IsNotNull(task, message, args);

        WaitForNotComplete(task, timeout, message, args);

        Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.Exception?.InnerException);
        Assert.IsFalse(task.IsCompleted, "Task.IsComplete. {0}", FormatMessage(message, args));
    }

    public static void IsNotComplete<T>(ValueTask<T> task, TimeSpan timeout, string message, params object[] args)
        => IsNotComplete(task.AsTask(), timeout, message, args);

    public static void IsNotComplete<T>(ValueTask<T> task, string message, params object[] args)
        => IsNotComplete(task, TimeSpan.Zero, message, args);

    public static void IsCanceled<T>(ValueTask<T> task, string message, params object[] args)
        => IsCanceled(task, TimeSpan.Zero, message, args);

    public static void IsCanceled<T>(ValueTask<T> task, TimeSpan timeout, string message, params object[] args)
        => IsCanceled(task.AsTask(), timeout, message, args);

    public static void IsCanceled(Task? task, string message, params object[] args)
        => IsCanceled(task, TimeSpan.Zero, message, args);

    public static void IsCanceled(Task? task, TimeSpan timeout, string message, params object[] args)
    {
        Assert.IsNotNull(task, message, args);

        WaitForComplete(task, timeout, message, args);

        Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.Exception?.InnerException);
        Assert.IsTrue(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
    }

    public static void IsFaulted<T>(ValueTask<T> task, Exception expected, string message, params object[] args)
        => IsFaulted(task, expected, TimeSpan.Zero, message, args);

    public static void IsFaulted<T>(ValueTask<T> task, Exception expected, TimeSpan timeout, string message, params object[] args)
        => IsFaulted(task.AsTask(), expected, timeout, message, args);

    public static void IsFaulted(Task? task, Exception expected, string message, params object[] args)
        => IsFaulted(task, expected, TimeSpan.Zero, message, args);

    public static void IsFaulted(Task? task, Exception expected, TimeSpan timeout, string message, params object[] args)
    {
        Assert.IsNotNull(task, message, args);

        WaitForComplete(task, timeout, message, args);

        Assert.IsTrue(task.IsCompleted, "Task.IsComplete. {0}", FormatMessage(message, args));

        Assert.IsFalse(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
        Assert.IsTrue(task.IsFaulted, "Task.IsFaulted. {0}", FormatMessage(message, args));

        Exception actual = task.Exception?.InnerException!;
        Assert.IsNotNull(actual, "Task.Exception. {0}", FormatMessage(message, args));
        Assert.IsInstanceOfType(actual, expected.GetType(), "Task.Exception. {0}", FormatMessage(message, args));
        Assert.AreEqual(expected.Message, actual.Message, "Task.Exception.Message. {0}", FormatMessage(message, args));
    }

    private static void WaitForComplete(Task task, TimeSpan timeout, string message, object[] args)
    {
        if (!task.IsCompleted && timeout > TimeSpan.Zero)
        {
            Assert.IsTrue(task.ContinueWith(_ => { }).Wait(timeout), "Task did not complete within {0}ms. {1}", timeout.TotalMilliseconds, FormatMessage(message, args));
        }
    }

    private static void WaitForNotComplete(Task task, TimeSpan timeout, string message, object[] args)
    {
        if (!task.IsCompleted && timeout > TimeSpan.Zero)
        {
            Assert.IsFalse(task.ContinueWith(_ => { }).Wait(timeout), "Task should not have completed within {0}ms. {1}", timeout.TotalMilliseconds, FormatMessage(message, args));
        }
    }

    private static object FormatMessage(string message, object[] args)
        => args.Length == 0 ? message : new FormattedMessage(message, args);

    private class FormattedMessage
    {
        private string _message;
        private object[] _args;

        public FormattedMessage(string message, object[] args)
        {
            _message = message;
            _args = args;
        }

        public override string ToString() => string.Format(_message, _args);
    }
}
