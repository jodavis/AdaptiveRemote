namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Interface for the test control service that runs in the host.
/// Used for bootstrapping test services via JSON-RPC.
/// </summary>
public interface ITestControlService
{
    /// <summary>
    /// Loads a test service from the specified assembly and type.
    /// </summary>
    Task<bool> LoadTestServiceAsync(string assemblyPath, string typeName);

    /// <summary>
    /// Invokes a method on the loaded test service.
    /// </summary>
    Task<object?> InvokeTestServiceAsync(string methodName, object?[]? args);
}
