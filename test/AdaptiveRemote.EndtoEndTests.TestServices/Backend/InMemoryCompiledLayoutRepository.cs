using System.Collections.Concurrent;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.EndToEndTests.TestServices.Backend;

/// <summary>
/// In-memory implementation of ICompiledLayoutRepository for testing without DynamoDB/LocalStack.
/// Thread-safe for concurrent test scenarios.
/// </summary>
public class InMemoryCompiledLayoutRepository : ICompiledLayoutRepository
{
    private readonly ConcurrentDictionary<Guid, CompiledLayout> _layouts = new();

    public Task<CompiledLayout?> GetActiveForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        CompiledLayout? activeLayout = _layouts.Values
            .FirstOrDefault(l => l.UserId == userId && l.IsActive);
        return Task.FromResult(activeLayout);
    }

    public Task<IReadOnlyList<CompiledLayout>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CompiledLayout> layouts = _layouts.Values
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CompiledAt)
            .ToList();
        return Task.FromResult(layouts);
    }

    public Task<CompiledLayout?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _layouts.TryGetValue(id, out CompiledLayout? layout);
        return Task.FromResult(layout);
    }

    public Task<CompiledLayout> SaveAsync(CompiledLayout layout, CancellationToken cancellationToken = default)
    {
        _layouts[layout.Id] = layout;
        return Task.FromResult(layout);
    }

    public Task SetActiveAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        // First, clear IsActive on all other layouts for this user
        foreach (Guid layoutId in _layouts.Keys)
        {
            if (_layouts.TryGetValue(layoutId, out CompiledLayout? layout) && layout.UserId == userId)
            {
                if (layout.IsActive && layoutId != id)
                {
                    _layouts[layoutId] = layout with { IsActive = false };
                }
            }
        }

        // Then, set the specified layout as active
        if (_layouts.TryGetValue(id, out CompiledLayout? targetLayout))
        {
            _layouts[id] = targetLayout with { IsActive = true };
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Test helper: clears all layouts from the in-memory store.
    /// </summary>
    public void Clear()
    {
        _layouts.Clear();
    }
}
