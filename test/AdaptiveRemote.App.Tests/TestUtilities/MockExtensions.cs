using System.Reflection;
using Moq;
using Moq.Language;
using Moq.Language.Flow;

namespace AdaptiveRemote.TestUtilities;

internal static class MockExtensions
{
    internal static IReturnsResult<ContractType> WithStandardTaskBehavior<ContractType>(
        this IReturnsThrows<ContractType, Task> setup,
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
        this IReturnsThrows<ContractType, Task<ReturnType>> setup,
        ReturnType returnValue)
        where ContractType : class
        => setup.WithStandardTaskBehavior(Task.FromResult(returnValue));

    internal static IReturnsResult<ContractType> WithStandardTaskBehavior<ContractType, ReturnType>(
        this IReturnsThrows<ContractType, ValueTask<ReturnType>> setup,
        ReturnType returnValue)
        where ContractType : class
        => setup.WithStandardTaskBehavior(Task.FromResult(returnValue));

    internal static IReturnsResult<ContractType> WithStandardTaskBehavior<ContractType, ReturnType>(
        this IReturnsThrows<ContractType, ValueTask<ReturnType>> setup,
        Task<ReturnType> returnValue)
        where ContractType : class
        => setup.Returns(CreateStandardReturnValue(returnValue).AsValueTask());

    internal static IReturnsResult<ContractType> WithStandardTaskBehavior<ContractType, ReturnType>(
        this IReturnsThrows<ContractType, Task<ReturnType>> setup,
        Task<ReturnType> returnTask)
        where ContractType : class
        => setup.Returns(CreateStandardReturnValue(returnTask));

    private static Func<IInvocation, Task<ReturnType>> CreateStandardReturnValue<ReturnType>(Task<ReturnType> returnTask)
    {
        return delegate (IInvocation invocation)
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
        };
    }

    internal static ICallbackResult WithArgumentValidation<ArgumentType>(
        this ICallback setup,
        string argumentName,
        Action<ArgumentType> validator)
        => setup.Callback(CreateValidatorCallback(argumentName, validator));

    internal static ICallbackResult WithArgumentValidation<ArgumentType>(
        this ICallback setup,
        string argumentName,
        ArgumentType expectedValue)
        => setup.WithArgumentValidation(argumentName, delegate (ArgumentType argumentValue)
        {
            Assert.AreEqual(expectedValue, argumentValue, string.Format("Argument '{0}' in {1}", argumentName, setup));
        });

    internal static IReturnsThrows<ContractType, ReturnType> WithArgumentValidation<ContractType, ReturnType, ArgumentType>(
        this ICallback<ContractType, ReturnType> setup,
        string argumentName,
        ArgumentType expectedValue)
        where ContractType : class
        => setup.WithArgumentValidation(argumentName, delegate (ArgumentType argumentValue)
        {
            Assert.AreEqual(expectedValue, argumentValue, string.Format("Argument '{0}' in {1}", argumentName, setup));
        });

    internal static IReturnsThrows<ContractType, ReturnType> WithArgumentValidation<ContractType, ReturnType, ArgumentType>(
        this ICallback<ContractType, ReturnType> setup,
        string argumentName,
        Action<ArgumentType> validator)
        where ContractType : class
        => setup.Callback(CreateValidatorCallback(argumentName, validator));

    private static Action<IInvocation> CreateValidatorCallback<ArgumentType>(string argumentName, Action<ArgumentType> validator)
    {
        return delegate (IInvocation invocation)
        {
            ParameterInfo[] parameters = invocation.Method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].Name == argumentName)
                {
                    object argumentValue = invocation.Arguments[i];
                    validator((ArgumentType)argumentValue);
                    return;
                }
            }

            Assert.Fail(string.Format("Did not find an argument '{0}' in call to {1}", argumentName, invocation.MatchingSetup.Expression));
        };
    }

    private static Func<IInvocation, ValueTask<ReturnType>> AsValueTask<ReturnType>(this Func<IInvocation, Task<ReturnType>> taskFunc)
    {
        return invocation => new(taskFunc(invocation));
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
