namespace AdaptiveRemote.TestUtilities;

internal static class TaskAssert
{
    public static void IsComplete(Task? task, string message, params object[] args)
    {
        Assert.IsNotNull(task, message, args);
        Assert.IsTrue(task.IsCompleted, "Task.IsComplete. {0}", FormatMessage(message, args));

        Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.Exception?.InnerException);
        Assert.IsFalse(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
    }

    public static void IsComplete(Task? task, int timeoutInMilliSeconds, string message, params object[] args)
    {
        Assert.IsNotNull(task, message, args);
        Assert.IsTrue(task.Wait(timeoutInMilliSeconds), "Task.IsComplete within {1}ms. {0}", FormatMessage(message, args), timeoutInMilliSeconds);

        Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.Exception?.InnerException);
        Assert.IsFalse(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
    }

    public static void IsComplete<T>(ValueTask<T> task, string message, params object[] args)
    {
        Assert.IsTrue(task.IsCompleted, "Task.IsComplete. {0}", FormatMessage(message, args));

        Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.AsTask().Exception?.InnerException);
        Assert.IsFalse(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
    }

    public static void IsComplete<T>(ValueTask<T> task, int timeoutInMilliseconds, string message, params object[] args)
    {
        Assert.IsTrue(task.AsTask().Wait(timeoutInMilliseconds), "Task.IsComplete within {1}ms. {0}", FormatMessage(message, args), timeoutInMilliseconds);

        Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.AsTask().Exception?.InnerException);
        Assert.IsFalse(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
    }

    public static void ResultEquals<T>(Task<T> task, T expected, string message, params object[] args)
    {
        Assert.IsNotNull(task, message, args);

        IsComplete(task, message, args);
        Assert.AreEqual(expected, task.Result, "Task.Result. {0}", FormatMessage(message, args));
    }

    public static void ResultEquals<T>(ValueTask<T> task, T expected, string message, params object[] args)
    {
        IsComplete(task, message, args);
        Assert.AreEqual(expected, task.Result, "Task.Result. {0}", FormatMessage(message, args));
    }

    public static void IsNotComplete(Task? task, string message, params object[] args)
    {
        Assert.IsNotNull(task, message, args);
        Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.Exception?.InnerException);
        Assert.IsFalse(task.IsCompleted, "Task.IsComplete. {0}", FormatMessage(message, args));
    }

    public static void IsNotComplete<T>(ValueTask<T> task, string message, params object[] args)
    {
        Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.AsTask().Exception?.InnerException);
        Assert.IsFalse(task.IsCompleted, "Task.IsComplete. {0}", FormatMessage(message, args));
    }

    public static void IsCanceled<T>(ValueTask<T> task, string message, params object[] args)
    {
        Assert.IsTrue(task.IsCompleted, "Task.IsComplete. {0}", FormatMessage(message, args));

        Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.AsTask().Exception?.InnerException);
        Assert.IsTrue(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
    }

    public static void IsCanceled(Task? task, string message, params object[] args)
    {
        Assert.IsNotNull(task, message, args);
        Assert.IsTrue(task.IsCompleted, "Task.IsComplete. {0}", FormatMessage(message, args));

        Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.Exception?.InnerException);
        Assert.IsTrue(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
    }

    public static void IsCanceled<T>(ValueTask<T> task, int timeoutInMilliseonds, string message, params object[] args)
    {
        Assert.IsNotNull(task, message, args);
        IsCanceled(task.AsTask(), timeoutInMilliseonds, message, args);
    }

    public static void IsCanceled(Task? task, int timeoutInMilliseonds, string message, params object[] args)
    {
        Assert.IsNotNull(task, message, args);
        Assert.IsTrue(task.ContinueWith(t => { }).Wait(timeoutInMilliseonds), "Task.IsComplete within {1}ms. {0}", FormatMessage(message, args), timeoutInMilliseonds);

        Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.Exception?.InnerException);
        Assert.IsTrue(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
    }

    public static void IsFaulted<T>(ValueTask<T> task, Exception expected, string message, params object[] args)
    {
        Assert.IsTrue(task.IsCompleted, "Task.IsComplete. {0}", FormatMessage(message, args));

        Assert.IsFalse(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
        Assert.IsTrue(task.IsFaulted, "Task.IsFaulted. {0}", FormatMessage(message, args));

        Exception actual = task.AsTask().Exception?.InnerException!;
        Assert.IsNotNull(actual, "Task.Exception. {0}", FormatMessage(message, args));
        Assert.IsInstanceOfType(actual, expected.GetType(), "Task.Exception. {0}", FormatMessage(message, args));
        Assert.AreEqual(expected.Message, actual.Message, "Task.Exception.Message. {0}", FormatMessage(message, args));
    }

    public static void IsFaulted(Task? task, Exception expected, string message, params object[] args)
    {
        Assert.IsNotNull(task, message, args);
        Assert.IsTrue(task.IsCompleted, "Task.IsComplete. {0}", FormatMessage(message, args));

        Assert.IsFalse(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
        Assert.IsTrue(task.IsFaulted, "Task.IsFaulted. {0}", FormatMessage(message, args));

        Exception actual = task.Exception?.InnerException!;
        Assert.IsNotNull(actual, "Task.Exception. {0}", FormatMessage(message, args));
        Assert.IsInstanceOfType(actual, expected.GetType(), "Task.Exception. {0}", FormatMessage(message, args));
        Assert.AreEqual(expected.Message, actual.Message, "Task.Exception.Message. {0}", FormatMessage(message, args));
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
