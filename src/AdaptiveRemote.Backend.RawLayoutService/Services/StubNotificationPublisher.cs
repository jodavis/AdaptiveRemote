using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.RawLayoutService.Services;

/// <summary>
/// Stub implementation of INotificationPublisher.
/// To be replaced with real SSE implementation in Task 9.
/// </summary>
public class StubNotificationPublisher : INotificationPublisher
{
    public Task PublishLayoutSavedAsync(string userId, Guid rawLayoutId, CancellationToken ct)
    {
        // No-op stub; notification wiring deferred to Task 9
        return Task.CompletedTask;
    }

    public Task PublishLayoutReadyAsync(string userId, Guid compiledLayoutId, CancellationToken ct)
    {
        // No-op stub; notification wiring deferred to Task 9
        return Task.CompletedTask;
    }
}
