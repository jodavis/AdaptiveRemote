using AdaptiveRemote.Services.Testing;
using FluentAssertions;

namespace AdaptiveRemote.EndtoEndTests;

public static class IApplicationTestServiceExtensions
{
    public static void WaitForPhase(this IApplicationTestService testService, LifecyclePhase expectedPhase, TimeSpan timeout)
    {
        LifecyclePhase? currentPhase = null;
        bool result = WaitHelpers.ExecuteWithRetries(() =>
        {
            currentPhase = testService.GetCurrentPhase();
            return currentPhase >= expectedPhase;
        }, timeout);

        currentPhase.Should().Be(expectedPhase,
            because: $"the test service should reach phase '{expectedPhase}' within {timeout.TotalSeconds}s.");
    }

    public static LifecyclePhase GetCurrentPhase(this IApplicationTestService testService)
    {
        return WaitHelpers.WaitForAsyncTask(testService.GetCurrentPhaseAsync);
    }

    public static void InvokeCommand(this IApplicationTestService testService, string commandName, int timeoutInSeconds = WaitHelpers.DefaultTimeoutInSeconds)
        => InvokeCommand(testService, commandName, TimeSpan.FromSeconds(timeoutInSeconds));

    public static void InvokeCommand(this IApplicationTestService testService, string commandName, TimeSpan timeout)
    {
        bool succeeded = WaitHelpers.WaitForAsyncTask(ct => testService.InvokeCommandAsync(commandName, ct), timeout);

        if (!succeeded)
        {
            throw new TimeoutException($"Invoking command '{commandName}' did not complete within {timeout.TotalSeconds}s.");
        }
    }
}
