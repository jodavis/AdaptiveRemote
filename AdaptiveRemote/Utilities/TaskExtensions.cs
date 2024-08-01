namespace AdaptiveRemote.Utilities;

internal static class TaskExtensions
{
    public static Task CancelWaitingOn(this Task task, CancellationToken cancellationToken)
    {
        TaskCompletionSource tcs = new();
        cancellationToken.Register(() => tcs.TrySetCanceled());
        task.ContinueWith(t => tcs.TrySetResult(), TaskContinuationOptions.ExecuteSynchronously);
        return tcs.Task;
    }

    public static Task<ResultType> CancelWaitingOn<ResultType>(this Task<ResultType> task, CancellationToken cancellationToken)
    {
        TaskCompletionSource<ResultType> tcs = new();
        cancellationToken.Register(() => tcs.TrySetCanceled());
        task.ContinueWith(t => tcs.TrySetResult(t.Result), TaskContinuationOptions.ExecuteSynchronously);
        return tcs.Task;
    }
}
