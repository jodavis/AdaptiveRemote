using System.Diagnostics.CodeAnalysis;

namespace AdaptiveRemote.Utilities;

internal static class TaskExtensions
{
    public static bool TryGetResultIfComplete<ResultType>(this Task<ResultType> task, [NotNullWhen(true)] out ResultType? result)
        where ResultType : notnull
    {
        if (task.IsCompleted)
        {
#pragma warning disable VSTHRD002 // Avoid async void methods -- we already confirmed the task is complete
            result = task.Result;
#pragma warning restore VSTHRD002 // Avoid async void methods
            return true;
        }

        result = default;
        return false;
    }
}
