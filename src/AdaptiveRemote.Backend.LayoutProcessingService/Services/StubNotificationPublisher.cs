using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Services;

/// <summary>
/// Stub implementation of INotificationPublisher.
/// No-op; real SSE notification wiring deferred to ADR-174 (Task 9).
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
