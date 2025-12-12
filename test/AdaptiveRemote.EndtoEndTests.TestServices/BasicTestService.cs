namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Basic implementation of ITestService for E2E testing.
/// </summary>
public class BasicTestService : ITestService
{
    private readonly Action? _shutdownCallback;

    public BasicTestService()
    {
    }

    public BasicTestService(Action shutdownCallback)
    {
        _shutdownCallback = shutdownCallback;
    }

    public Task<string> ExecuteTestAsync(string testData)
    {
        return Task.FromResult($"Echo: {testData}");
    }

    public Task RequestShutdownAsync()
    {
        _shutdownCallback?.Invoke();
        return Task.CompletedTask;
    }
}
