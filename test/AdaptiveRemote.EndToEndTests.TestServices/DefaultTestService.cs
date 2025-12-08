using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.EndToEndTests.TestServices;

/// <summary>
/// Default implementation of ITestService for end-to-end testing.
/// This service can be dynamically loaded into the host application to control it during tests.
/// </summary>
public class DefaultTestService : ITestService
{
    private readonly IHostApplicationLifetime? _lifetime;

    public string ServiceName => "DefaultTestService";

    /// <summary>
    /// Creates a new instance of DefaultTestService.
    /// </summary>
    /// <param name="lifetime">Optional host application lifetime for shutdown control.</param>
    public DefaultTestService(IHostApplicationLifetime? lifetime = null)
    {
        _lifetime = lifetime;
    }

    public Task InitializeAsync()
    {
        // Initialization logic if needed
        return Task.CompletedTask;
    }

    public Task RequestShutdownAsync()
    {
        // Request application shutdown via the host lifetime
        _lifetime?.StopApplication();
        return Task.CompletedTask;
    }

    public Task<bool> HealthCheckAsync()
    {
        return Task.FromResult(true);
    }
}
