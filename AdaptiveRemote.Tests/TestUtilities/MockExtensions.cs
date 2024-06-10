using Moq.Language.Flow;

namespace AdaptiveRemote.TestUtilities;

internal static class MockExtensions
{
    internal static IReturnsResult<ContractType> WithStandardTaskBehavior<ContractType>(
        this ISetup<ContractType, Task> setup,
        Task? returnTask = default)
        where ContractType : class
    {
        return setup.Returns(delegate (CancellationToken cancel)
        {
            TaskCompletionSource tcs = new();
            if (returnTask is not null)
            {
                returnTask.ContinueWith(t => t.IsFaulted ? tcs.TrySetException(t.Exception) : tcs.TrySetResult(), TaskContinuationOptions.ExecuteSynchronously);
            }
            else
            {
                // Default to completed task
                tcs.SetResult();
            }

            cancel.Register(() => tcs.TrySetCanceled());

            return tcs.Task;
        });
    }
}
