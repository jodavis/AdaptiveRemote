namespace AdaptiveRemote.Contracts;

/// <summary>
/// CRUD interface for raw layouts. Used by the editor application to manage layouts.
/// </summary>
public interface IRawLayoutRepository
{
    Task<RawLayout?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<RawLayout>> ListByUserAsync(string userId, CancellationToken ct);
    Task<RawLayout> SaveAsync(RawLayout layout, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
