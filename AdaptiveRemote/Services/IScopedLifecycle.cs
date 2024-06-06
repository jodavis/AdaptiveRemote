using Microsoft.Extensions.DependencyInjection;

namespace AdaptiveRemote.Services;

internal interface IScopedLifecycle
{
    string Name { get; }

    Task InitializeAsync(CancellationToken cancellationToken);
}
