namespace AdaptiveRemote.Services;

internal interface IScopedLifecycle
{
    string Name { get; }

    Task InitializeAsync(CancellationToken cancellationToken);
    Task CleanUpAsync(CancellationToken cancellationToken);
}
