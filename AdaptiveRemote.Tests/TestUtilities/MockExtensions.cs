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

    internal static IReturnsResult<ContractType> WithStandardTaskBehavior<ContractType, ReturnType>(
        this ISetup<ContractType, Task<ReturnType>> setup,
        ReturnType returnValue)
        where ContractType : class
        => setup.WithStandardTaskBehavior(Task.FromResult(returnValue));

    internal static IReturnsResult<ContractType> WithStandardTaskBehavior<ContractType, ReturnType>(
        this ISetup<ContractType, Task<ReturnType>> setup,
        Task<ReturnType> returnTask)
        where ContractType : class
    {
        return setup.Returns(delegate (IInvocation invocation)
        {
            TaskCompletionSource<ReturnType> tcs = new();
            returnTask.ContinueWith(t => t.IsFaulted ? tcs.TrySetException(t.Exception.InnerException ?? t.Exception) : tcs.TrySetResult(t.Result), TaskContinuationOptions.ExecuteSynchronously);

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

    internal static CancellationToken WithExpectedCancellation<ContractType>(
        this ISetup<ContractType, Task> setup,
        bool throwWhenCancelled)
        where ContractType : class
    {
        TaskCompletionSource tcs = new();
        Action onCancelled = throwWhenCancelled
            ? tcs.SetCanceled
            : () => { };

        setup
            .Callback(ExpectCancellation(onCancelled, out CancellationToken result))
            .Returns(tcs.Task);

        return result;
    }

    internal static CancellationToken WithExpectedCancellation<ContractType, ResultType>(
        this ISetup<ContractType, Task<ResultType>> setup,
        bool throwWhenCancelled)
        where ContractType : class
    {
        TaskCompletionSource<ResultType> tcs = new();
        Action onCancelled = throwWhenCancelled
            ? tcs.SetCanceled
            : () => { };

        setup
            .Callback(ExpectCancellation(onCancelled, out CancellationToken result))
            .Returns(tcs.Task);

        return result;
    }

    private static InvocationAction ExpectCancellation(Action onCancelled, out CancellationToken result)
    {
        CancellationTokenSource cts = new();
        result = cts.Token;

        return new(delegate (IInvocation invocation)
        {
            foreach (object argument in invocation.Arguments)
            {
                if (argument is CancellationToken cancel)
                {
                    cancel.Register(() =>
                    {
                        cts.Cancel();
                        onCancelled();
                    });
                    break;
                }
            }
        });
    }
}
