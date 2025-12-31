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

    public static void InvokeCommand(this IApplicationTestService testService, string commandName)
    {
        bool succeeded = WaitHelpers.WaitForAsyncTask(ct => testService.InvokeCommandAsync(commandName, ct));

        if (!succeeded)
        {
            throw new TimeoutException($"Invoking command '{commandName}' timed out.");
        }
    }
}
