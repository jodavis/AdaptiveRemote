using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Commands;

internal class NullCommandService<CommandType> : CommandServiceBase<CommandType>
    where CommandType : Command
{
    public NullCommandService(IRemoteDefinitionService remoteDefinition, ILogger<NullCommandService<CommandType>> logger)
        : base($"Null {typeof(CommandType).Name}s", remoteDefinition)
    {
        Logger = logger;
    }

    protected override ILogger Logger { get; }

    protected override Command.ExecuteDelegate CreateHandler(CommandType command)
    {
        return _ => Task.CompletedTask;
    }
}
