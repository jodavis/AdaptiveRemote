using PolyType;
using StreamJsonRpc;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Interface for test services that can be loaded dynamically by the host during E2E testing.
/// </summary>
[RpcMarshalable]
[JsonRpcContract]
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IApplicationTestService : IDisposable
{
    /// <summary>
    /// Gets the current lifecycle phase of the application.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the wait operation.</param>
    /// <returns>A task that completes when the current phase has been fetched.</returns>
    Task<LifecyclePhase> GetCurrentPhaseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a command in the host application by its command ID.
    /// </summary>
    /// <param name="commandId">The unique identifier of the command to invoke (e.g., "Exit").</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that completes when the command has been invoked.</returns>
    Task InvokeCommandAsync(string commandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells the application host to shut down
    /// </summary>
    Task StopApplicationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the conversation system is currently in listening mode.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the conversation system is listening, false otherwise.</returns>
    Task<bool> GetIsListeningAsync(CancellationToken cancellationToken = default);
}
