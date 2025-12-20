using StreamJsonRpc;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Interface for test services that can be loaded dynamically by the host during E2E testing.
/// </summary>
[RpcMarshalable]
public interface ITestService : IDisposable
{
    /// <summary>
    /// Waits for the application to reach the specified lifecycle phase.
    /// </summary>
    /// <param name="phase">The lifecycle phase to wait for (e.g., Ready).</param>
    /// <param name="cancellationToken">Cancellation token for the wait operation.</param>
    /// <returns>A task that completes when the specified phase is reached.</returns>
    Task WaitForPhaseAsync(LifecyclePhase phase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a command in the host application by its command ID.
    /// </summary>
    /// <param name="commandId">The unique identifier of the command to invoke (e.g., "Exit").</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that completes when the command has been invoked.</returns>
    Task InvokeCommandAsync(string commandId, CancellationToken cancellationToken = default);
}
