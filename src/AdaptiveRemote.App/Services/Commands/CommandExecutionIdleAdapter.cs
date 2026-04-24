namespace AdaptiveRemote.Services.Commands;

// Creates one CommandIdleAdapter per command and delegates lifecycle calls to them.
internal class CommandExecutionIdleAdapter : IUserActivityDetector
{
    private readonly IReadOnlyList<IUserActivityDetector> _adapters;

    public DateTime LastActivityTime => _adapters.Select(x => x.LastActivityTime).Max();

    public CommandExecutionIdleAdapter(IRemoteDefinitionService remoteDefinition)
    {
        _adapters = remoteDefinition.GetCommands()
            .Select(cmd => new CommandIdleAdapter(cmd))
            .ToList();
    }
}
