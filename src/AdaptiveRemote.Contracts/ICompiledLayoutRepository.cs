namespace AdaptiveRemote.Contracts;

/// <summary>
/// Repository interface for compiled layout storage and retrieval.
/// </summary>
public interface ICompiledLayoutRepository
{
    /// <summary>
    /// Gets the active compiled layout for the specified user.
    /// </summary>
    Task<CompiledLayout?> GetActiveForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all compiled layouts for the specified user.
    /// </summary>
    Task<IReadOnlyList<CompiledLayout>> ListByUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific compiled layout by ID.
    /// </summary>
    Task<CompiledLayout?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a compiled layout. Creates or replaces the record.
    /// </summary>
    Task<CompiledLayout> SaveAsync(CompiledLayout layout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the specified compiled layout as active for the user, clearing
    /// IsActive on all other layouts for the same user.
    /// </summary>
    Task SetActiveAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
