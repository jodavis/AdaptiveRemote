namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Interface for the test control service that runs in the host.
/// Used for bootstrapping test services via JSON-RPC.
/// </summary>
public interface ITestControlService
{
    /// <summary>
    /// Dynamically loads a test service from the specified assembly and type.
    /// The test service is instantiated within the application's DI scope to access scoped services.
    /// </summary>
    /// <param name="assemblyPath">Full path to the assembly containing the test service type.</param>
    /// <param name="typeName">Fully qualified name of the test service type to instantiate.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A proxy to the test service that can be used to invoke test commands.</returns>
    Task<ITestService> CreateTestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken);
}
