namespace AdaptiveRemote.Contracts;

/// <summary>
/// RawLayoutService → LayoutProcessingService — SQS-backed; enqueues a message on layout save
/// </summary>
public interface ILayoutProcessingTrigger
{
    Task TriggerAsync(Guid rawLayoutId, CancellationToken ct);
}
