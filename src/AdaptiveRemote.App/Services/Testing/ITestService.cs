using StreamJsonRpc;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Interface for test services that can be loaded dynamically by the host during E2E testing.
/// </summary>
[RpcMarshalable]
public interface ITestService : IDisposable
{
    Task WaitForPhaseAsync(LifecyclePhase phase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests the host to initiate a clean shutdown.
    /// </summary>
    Task InvokeCommandAsync(string commandId, CancellationToken cancellationToken = default);
}
