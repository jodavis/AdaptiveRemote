using AdaptiveRemote.Services.Testing;
using FluentAssertions;

namespace AdaptiveRemote.EndtoEndTests;

public static class ITestServiceExtensions
{
    public static void WaitForPhase(this ITestService testService, LifecyclePhase expectedPhase, TimeSpan timeout)
    {
        LifecyclePhase? currentPhase = null;
        bool result = WaitUtilities.ExecuteWithRetries(() =>
        {
            currentPhase = testService.GetCurrentPhase();
            return currentPhase >= expectedPhase;
        }, timeout);

        currentPhase.Should().Be(expectedPhase,
            because: $"the test service should reach phase '{expectedPhase}' within {timeout.TotalSeconds}s.");
    }

    public static LifecyclePhase GetCurrentPhase(this ITestService testService)
    {
        return WaitUtilities.WaitForAsyncTask(testService.GetCurrentPhaseAsync);
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
