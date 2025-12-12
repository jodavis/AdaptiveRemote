namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Interface for test services that can be loaded dynamically by the host during E2E testing.
/// </summary>
public interface ITestService
{
    /// <summary>
    /// Performs a test operation and returns a result.
    /// </summary>
    Task<string> ExecuteTestAsync(string testData);

    /// <summary>
    /// Requests the host to initiate a clean shutdown.
    /// </summary>
    Task RequestShutdownAsync();
}
