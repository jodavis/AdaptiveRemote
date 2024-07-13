using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Commands;

internal class CommandService : ICommandService
{
    private readonly IApplicationService _application;
    private readonly IBroadlinkService _broadlink;
    private readonly ILogger<CommandService> _logger;

    private readonly IReadOnlyDictionary<string, Action<IApplicationService>> _applicationStuff = new Dictionary<string, Action<IApplicationService>>()
    {
        [nameof(IApplicationService.Exit)] = app => app.Exit()
    };

    public CommandService(IApplicationService application, IBroadlinkService broadlink, ILogger<CommandService> logger, IRemoteDefinitionService definitionService)
    {
        _application = application;
        _broadlink = broadlink;
        _logger = logger;

        foreach (Command command in definitionService.GetCommands())
        {
            if (command is ApplicationCommand)
            {
                command.ExecuteAsync = cancellationToken => ((ICommandService)this).ExecuteAsync(command, cancellationToken);
                command.IsEnabled = true;
            }
        }
    }

    async Task ICommandService.ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(Message.CommandService_Executing, command);
            switch (command)
            {
                case ApplicationCommand applicationCommand:
                    _applicationStuff[applicationCommand.Name].Invoke(_application);
                    break;
                case BroadlinkCommand broadlinkCommand:
                    await _broadlink.SendAsync(cancellationToken);
                    break;
                default:
                    throw Errors.Commands_UnsupportedCommandType(command, nameof(command));
            }
            _logger.LogInformation(Message.CommandService_Executed, command);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(Message.CommandService_Cancelled, command);
            throw;
        }
        catch (Exception error)
        {
            _logger.LogError(Message.CommandService_Error, command, error);
            throw;
        }
    }
}
