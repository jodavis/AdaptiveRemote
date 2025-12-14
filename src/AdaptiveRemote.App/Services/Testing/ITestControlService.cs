namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Interface for the test control service that runs in the host.
/// Used for bootstrapping test services via JSON-RPC.
/// </summary>
public interface ITestControlService
{
    Task<ITestService> CreateTestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken);
}
