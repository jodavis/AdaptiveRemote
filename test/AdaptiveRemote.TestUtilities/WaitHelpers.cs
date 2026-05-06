namespace AdaptiveRemote.TestUtilities;

public static class WaitHelpers
{
    public const int DefaultTimeoutInSeconds = 5;

    public static bool ExecuteWithRetries(Func<bool> action, int timeoutInSeconds = DefaultTimeoutInSeconds)
        => ExecuteWithRetries(action, TimeSpan.FromSeconds(timeoutInSeconds));

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

    public static bool ExecuteWithRetries(Func<CancellationToken, Task<bool>> action, int timeoutInSeconds = DefaultTimeoutInSeconds)
        => WaitForState(action, true, TimeSpan.FromSeconds(timeoutInSeconds));

    public static bool ExecuteWithRetries(Func<CancellationToken, Task<bool>> action, TimeSpan timeout)
        => WaitForState(action, true, timeout);

    public static bool WaitForAsyncTask(Func<CancellationToken, Task> action, int timeoutInSeconds = DefaultTimeoutInSeconds)
        => WaitForAsyncTask(action, TimeSpan.FromSeconds(timeoutInSeconds));

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

    public static ResultType WaitForAsyncTask<ResultType>(Func<CancellationToken, Task<ResultType>> action, int timeoutInSeconds = DefaultTimeoutInSeconds)
        => WaitForAsyncTask(action, TimeSpan.FromSeconds(timeoutInSeconds));

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

    public static bool WaitForState<StateType>(Func<CancellationToken, Task<StateType>> stateFunc, StateType expectedState, int timeoutInSeconds = DefaultTimeoutInSeconds)
        where StateType : struct
        => WaitForState(stateFunc, expectedState, TimeSpan.FromSeconds(timeoutInSeconds));

    public static bool WaitForState<StateType>(Func<CancellationToken, Task<StateType>> stateFunc, StateType expectedValue, TimeSpan timeout)
        where StateType : struct
    {
        DateTime endTime = DateTime.UtcNow.Add(timeout);

        return ExecuteWithRetries(() =>
        {
            StateType result = default;

            bool completed = WaitForAsyncTask(
                async cancellationToken => { result = await stateFunc(cancellationToken); },
                endTime - DateTime.UtcNow);

            return completed && result.Equals(expectedValue);
        }, timeout);
    }
}
