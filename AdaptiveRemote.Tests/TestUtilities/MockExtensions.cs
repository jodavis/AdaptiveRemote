using Moq;
using Moq.Language.Flow;

namespace AdaptiveRemote.TestUtilities;

internal static class MockExtensions
{
    internal static IReturnsResult<ContractType> WithStandardTaskBehavior<ContractType>(
        this ISetup<ContractType, Task> setup,
        Task? returnTask = default)
        where ContractType : class
    {
        return setup.Returns(delegate (IInvocation invocation)
        {
            TaskCompletionSource tcs = new();
            if (returnTask is not null)
            {
                returnTask.ContinueWith(t => t.IsFaulted ? tcs.TrySetException(t.Exception.InnerException ?? t.Exception) : tcs.TrySetResult(), TaskContinuationOptions.ExecuteSynchronously);
            }
            else
            {
                // Default to completed task
                tcs.SetResult();
            }

            foreach (object argument in invocation.Arguments)
            {
                if (argument is CancellationToken cancel)
                {
                    cancel.Register(() => tcs.TrySetCanceled());
                    break;
                }
            }

            return tcs.Task;
        });
    }
}
