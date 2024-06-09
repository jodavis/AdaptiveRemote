using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Commands;

internal class CommandService : ICommandService
{
    private readonly IApplicationService _application;
    private readonly ITiVoService _tivo;
    private readonly IBroadlinkService _broadlink;
    private readonly ILogger<CommandService> _logger;

    private readonly IReadOnlyDictionary<string, Action<IApplicationService>> _applicationStuff = new Dictionary<string, Action<IApplicationService>>()
    {
        [nameof(IApplicationService.Exit)] = app => app.Exit()
    };

    public CommandService(IApplicationService application, ITiVoService tivo, IBroadlinkService broadlink, ILogger<CommandService> logger)
    {
        _application = application;
        _tivo = tivo;
        _broadlink = broadlink;
        _logger = logger;
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
                case TiVoCommand tivoCommand:
                    await _tivo.SendAsync(tivoCommand.CommandId, cancellationToken);
                    break;
                case BroadlinkCommand broadlinkCommand:
                    await _broadlink.SendAsync(cancellationToken);
                    break;
                default:
                    throw new ArgumentException(Phrases.Commands_UnsupportedCommandType(command), nameof(command));
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
