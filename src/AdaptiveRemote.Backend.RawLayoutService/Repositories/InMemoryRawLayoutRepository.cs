using System.Collections.Concurrent;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.RawLayoutService.Repositories;

/// <summary>
/// In-memory implementation of IRawLayoutRepository and IRawLayoutStatusWriter for testing
/// without DynamoDB. Thread-safe for concurrent access.
/// Used when DynamoDB__ServiceUrl is set to "in-memory" or when DynamoDB is not configured.
/// </summary>
public class InMemoryRawLayoutRepository : IRawLayoutRepository, IRawLayoutStatusWriter
{
    private readonly ConcurrentDictionary<Guid, RawLayout> _layouts = new();

    public Task<RawLayout?> GetAsync(Guid id, CancellationToken ct)
    {
        _layouts.TryGetValue(id, out RawLayout? layout);
        return Task.FromResult(layout);
    }

    public Task<IReadOnlyList<RawLayout>> ListByUserAsync(string userId, CancellationToken ct)
    {
        IReadOnlyList<RawLayout> layouts = _layouts.Values
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.UpdatedAt)
            .ToList();
        return Task.FromResult(layouts);
    }

    public Task<RawLayout> SaveAsync(RawLayout layout, CancellationToken ct)
    {
        // Ensure Id is set (generate if needed for new layouts)
        RawLayout savedLayout = layout.Id == Guid.Empty
            ? layout with { Id = Guid.NewGuid() }
            : layout;

        _layouts[savedLayout.Id] = savedLayout;
        return Task.FromResult(savedLayout);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct)
    {
        _layouts.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task UpdateValidationResultAsync(Guid rawLayoutId, ValidationResult result, CancellationToken ct)
    {
        if (_layouts.TryGetValue(rawLayoutId, out RawLayout? layout))
        {
            RawLayout updated = layout with { ValidationResult = result };
            _layouts[rawLayoutId] = updated;
        }

        return Task.CompletedTask;
    }
}
