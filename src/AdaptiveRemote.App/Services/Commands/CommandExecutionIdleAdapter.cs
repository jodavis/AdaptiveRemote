namespace AdaptiveRemote.Services.Commands;

// Creates one CommandIdleAdapter per command and delegates lifecycle calls to them.
internal class CommandExecutionIdleAdapter : IScopedLifecycle
{
    private readonly IReadOnlyList<CommandIdleAdapter> _adapters;

    public string Name => "Command execution idle adapter";

    public CommandExecutionIdleAdapter(IRemoteDefinitionService remoteDefinition, IIdleDetector idleDetector)
    {
        _adapters = remoteDefinition.GetCommands()
            .Select(cmd => new CommandIdleAdapter(cmd, idleDetector))
            .ToList();
    }

    public Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
        => Task.WhenAll(_adapters.Select(a => a.InitializeAsync(activity, cancellationToken)));

    public Task CleanUpAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
        => Task.WhenAll(_adapters.Select(a => a.CleanUpAsync(activity, cancellationToken)));
}
