using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Impl;

internal class CommandExecutionService : ICommandExecutionService
{
    private readonly ILogger<CommandExecutionService> _logger;

    public CommandExecutionService(ILogger<CommandExecutionService> logger)
    {
        _logger = logger;
    }

    async Task ICommandExecutionService.ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing {Name} {ID}", command.GetType().Name, command.CSSID);

        if (command is ApplicationCommand)
        {
            System.Windows.Application.Current.Shutdown();
        }

        command.IsActive = true;
        await Task.Delay(600, cancellationToken);
        command.IsActive = false;
    }
}
