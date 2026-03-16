using AdaptiveRemote.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

internal class LifecycleCommandService : CommandServiceBase<LifecycleCommand>
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILifecycleViewController _viewController;

    public LifecycleCommandService(IHostApplicationLifetime applicationLifetime, ILifecycleViewController viewController, IRemoteDefinitionService remoteDefinition, ILogger<LifecycleCommandService> logger)
        : base("Application Commands", remoteDefinition, logger)
    {
        _applicationLifetime = applicationLifetime;
        _viewController = viewController;
    }

    protected override Command.ExecuteDelegate CreateHandler(LifecycleCommand command)
        => command.Name switch
        {
            "Exit" => ExitAsync,
            "Learn" => delegate (CancellationToken _)
            {
                _viewController.EnterLearningMode();
                return Task.CompletedTask;
            }
            ,
            _ => throw new Exception($"Unknown {command}")
        };

    protected override Command.ExecuteDelegate? CreateProgramHandler(LifecycleCommand command)
        => command.Name switch
        {
            "Exit" => ExitAsync,
            "Learn" => async delegate (CancellationToken _)
            {
                await _viewController.ExitLearningModeAsync();
            }
            ,
            _ => null
        };

    private Task ExitAsync(CancellationToken _)
    {
        _viewController.SetPhase(LifecyclePhase.Stopping);
        _applicationLifetime.StopApplication();
        return Task.CompletedTask;
    }
}
