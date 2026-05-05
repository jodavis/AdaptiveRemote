namespace AdaptiveRemote.Contracts;

/// <summary>
/// NotificationService — called by RawLayoutService on save, and by LayoutProcessingService on publish.
/// SSE event types:
///   layout-saved → editor subscribes; used to detect concurrent saves on the same layout
///   layout-ready → client subscribes; triggers download of the new compiled layout
/// </summary>
public interface INotificationPublisher
{
    Task PublishLayoutSavedAsync(string userId, Guid rawLayoutId, CancellationToken ct);
    Task PublishLayoutReadyAsync(string userId, Guid compiledLayoutId, CancellationToken ct);
}
