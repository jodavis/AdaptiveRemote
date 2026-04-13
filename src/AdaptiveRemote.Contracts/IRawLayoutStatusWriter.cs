namespace AdaptiveRemote.Contracts;

/// <summary>
/// Narrow write-back interface for LayoutProcessingService to record compilation results
/// on a raw layout without requiring full CRUD access to RawLayoutService.
/// </summary>
public interface IRawLayoutStatusWriter
{
    Task UpdateValidationResultAsync(Guid rawLayoutId, ValidationResult result, CancellationToken ct);
}
