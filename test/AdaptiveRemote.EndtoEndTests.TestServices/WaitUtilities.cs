namespace AdaptiveRemote.EndtoEndTests;

public static class WaitUtilities
{
    public const int DefaultTimeoutInMilliseconds = 1000;

    public static bool ExecuteWithRetries(Func<bool> action, int timeoutInMilliseconds = DefaultTimeoutInMilliseconds)
        => ExecuteWithRetries(action, TimeSpan.FromMilliseconds(timeoutInMilliseconds));

    public static bool ExecuteWithRetries(Func<bool> action, TimeSpan timeout)
    {
        DateTime endTime = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < endTime)
        {
            if (action())
            {
                return true;
            }
            Thread.Sleep(100);
        }
        return false;
    }

    public static bool ExecuteWithRetries(Func<CancellationToken, Task<bool>> action, int timeoutInMilliseconds = DefaultTimeoutInMilliseconds)
        => ExecuteWithRetries(action, TimeSpan.FromMilliseconds(timeoutInMilliseconds));

    public static bool ExecuteWithRetries(Func<CancellationToken, Task<bool>> action, TimeSpan timeout)
    {
        DateTime endTime = DateTime.UtcNow.Add(timeout);

        return ExecuteWithRetries(() =>
        {
            bool result = false;

            WaitForAsyncTask(
                async cancellationToken => { result = await action(cancellationToken); },
                endTime - DateTime.UtcNow);

            return result;
        });
    }

    public static bool WaitForAsyncTask(Func<CancellationToken, Task> action, int timeoutInMilliseconds = DefaultTimeoutInMilliseconds)
        => WaitForAsyncTask(action, TimeSpan.FromMilliseconds(timeoutInMilliseconds));

    public static bool WaitForAsyncTask(Func<CancellationToken, Task> action, TimeSpan timeout)
    {
        CancellationTokenSource cts = new(timeout);
        Task task = action(cts.Token);

        try
        {
            if (task.Wait(timeout))
            {
                return true;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Canceled due to timeout
        }

        return false;
    }

    public static ResultType WaitForAsyncTask<ResultType>(Func<CancellationToken, Task<ResultType>> action, int timeoutInMilliseconds = DefaultTimeoutInMilliseconds)
        => WaitForAsyncTask(action, TimeSpan.FromMilliseconds(timeoutInMilliseconds));

    public static ResultType WaitForAsyncTask<ResultType>(Func<CancellationToken, Task<ResultType>> action, TimeSpan timeout)
    {
        ResultType? result = default;
        bool completed = WaitForAsyncTask(
            async cancellationToken => { result = await action(cancellationToken); },
            timeout);

        if (!completed)
        {
            throw new TimeoutException($"Task did not complete within {timeout}");
        }

        return result!;
    }
}
