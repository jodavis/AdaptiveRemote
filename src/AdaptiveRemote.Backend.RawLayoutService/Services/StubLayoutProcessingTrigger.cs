using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.RawLayoutService.Services;

/// <summary>
/// Stub implementation of ILayoutProcessingTrigger.
/// To be replaced with real SQS implementation in Task 5.
/// </summary>
public class StubLayoutProcessingTrigger : ILayoutProcessingTrigger
{
    public Task TriggerAsync(Guid rawLayoutId, CancellationToken ct)
    {
        // No-op stub; SQS wiring deferred to Task 5
        return Task.CompletedTask;
    }
}
