namespace AdaptiveRemote.EndToEndTests.TestServices;

/// <summary>
/// Interface for test services that can be dynamically loaded into the host application.
/// This interface is exposed via JSON-RPC without requiring compile-time dependencies.
/// </summary>
public interface ITestService
{
    /// <summary>
    /// Gets the name of the test service for identification.
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Initializes the test service with the host application context.
    /// </summary>
    /// <returns>A task that completes when initialization is done.</returns>
    Task InitializeAsync();

    /// <summary>
    /// Requests the host application to shut down cleanly.
    /// </summary>
    /// <returns>A task that completes when the shutdown request is acknowledged.</returns>
    Task RequestShutdownAsync();

    /// <summary>
    /// Performs a simple health check to verify the service is responsive.
    /// </summary>
    /// <returns>True if the service is healthy.</returns>
    Task<bool> HealthCheckAsync();
}
