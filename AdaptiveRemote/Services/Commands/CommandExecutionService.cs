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
        _logger.LogInformation("Executing {Name} {ID}", command.GetType().Name, command.CSSID);

        if (command is ApplicationCommand)
        {
            System.Windows.Application.Current.Shutdown();
        }

        _ = DoCommandFeedbackAsync(command);

        return Task.CompletedTask;
    }

    private static async Task DoCommandFeedbackAsync(Command command)
    {
        command.IsActive = true;
        await Task.Delay(600);
        command.IsActive = false;
    }
}
