using AdaptiveRemote.Services.Testing;

namespace AdaptiveRemote.EndtoEndTests;

public static class ITestServiceExtensions
{
    public static void WaitForPhase(this ITestService testService, LifecyclePhase phase, TimeSpan timeout)
    {
        bool succeeded = WaitUtilities.WaitForAsyncTask(ct => testService.WaitForPhaseAsync(phase, ct), timeout);

        if (!succeeded)
        {
            throw new TimeoutException($"Waiting for phase '{phase}' timed out after {timeout.TotalMilliseconds} ms.");
        }
    }

    public static void InvokeCommand(this ITestService testService, string commandName)
    {
        bool succeeded = WaitUtilities.WaitForAsyncTask(ct => testService.InvokeCommandAsync(commandName, ct));

        if (!succeeded)
        {
            throw new TimeoutException($"Invoking command '{commandName}' timed out.");
        }
    }
}
