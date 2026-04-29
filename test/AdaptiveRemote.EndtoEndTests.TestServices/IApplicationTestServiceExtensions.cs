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
            return currentPhase == expectedPhase || currentPhase == LifecyclePhase.FatalError;
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

    public static void StopApplication(this IApplicationTestService testService, TimeSpan timeout)
    {
        bool succeeded = WaitHelpers.WaitForAsyncTask(testService.StopApplicationAsync, timeout);

        if (!succeeded)
        {
            throw new TimeoutException($"Timed out waiting for {nameof(testService.StopApplicationAsync)} after {timeout.TotalSeconds}s");
        }
    }

    /// <summary>
    /// Waits for the conversation system to reach the expected listening state.
    /// Polls the listening state until it matches the expected value or the timeout is reached.
    /// </summary>
    /// <param name="testService">The test service to query.</param>
    /// <param name="expected">The expected listening state (true for listening, false for not listening).</param>
    /// <param name="timeoutInSeconds">Maximum time to wait in seconds.</param>
    public static void WaitForIsListening(this IApplicationTestService testService, bool expected, int timeoutInSeconds)
    {
        bool? currentState = null;
        bool result = WaitHelpers.ExecuteWithRetries(() =>
        {
            currentState = WaitHelpers.WaitForAsyncTask(testService.GetIsListeningAsync);
            return currentState == expected;
        }, TimeSpan.FromSeconds(timeoutInSeconds));

        currentState.Should().Be(expected,
            because: $"the conversation system should be {(expected ? "listening" : "not listening")} within {timeoutInSeconds}s.");
    }
}
