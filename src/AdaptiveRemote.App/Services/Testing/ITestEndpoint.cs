using PolyType;
using StreamJsonRpc;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Interface for the test control service that runs in the host.
/// Used for bootstrapping test services via JSON-RPC and coordinating the host startup flow.
/// </summary>
[RpcMarshalable]
[JsonRpcContract]
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface ITestEndpoint
{
    /// <summary>
    /// Loads a test service into the host's service collection.
    /// This must be called before BuildAndRunHostAsync.
    /// </summary>
    /// <param name="contractType">Fully qualified name of the service interface type.</param>
    /// <param name="serviceName">Fully qualified name of the implementation type.</param>
    /// <param name="serviceAssembly">Full path to the assembly containing the service type.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task AddTestServiceAsync(string contractType, string serviceName, string serviceAssembly, CancellationToken cancellationToken);

    /// <summary>
    /// Unblocks the host process's startup sequence.
    /// After calling this method, the host will build and start running.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task BuildAndRunHostAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Attempt to cleanly shut down the host process.
    /// If the host has been run, this should cause a normal shutdown via IHostApplicationLifetime.
    /// Otherwise, this should abort the startup sequence.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task StopApplicationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Requests an API for loading test services and retrieving contract interfaces.
    /// This can only be called after BuildAndRunHostAsync, since the host's service provider is not available until then.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A proxy to the test service provider.</returns>
    Task<ITestServiceProvider> GetTestServiceProviderAsync(CancellationToken cancellationToken);
}
