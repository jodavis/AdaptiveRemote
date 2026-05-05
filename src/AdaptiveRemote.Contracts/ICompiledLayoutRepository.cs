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
}
