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

    Task ICommandExecutionService.ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing {Name} {ID}", command.GetType().Name, command.ID);

        if (command is ExitCommand)
        {
            System.Windows.Application.Current.Shutdown();
        }

        return Task.CompletedTask;
    }
}
