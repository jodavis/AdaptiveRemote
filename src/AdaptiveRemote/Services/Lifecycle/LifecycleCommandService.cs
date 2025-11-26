using AdaptiveRemote.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

internal class LifecycleCommandService : CommandServiceBase<LifecycleCommand>
{
    private readonly IHostApplicationLifetime _applicationLifetime;

    public LifecycleCommandService(IHostApplicationLifetime applicationLifetime, IRemoteDefinitionService remoteDefinition, ILogger<LifecycleCommandService> logger)
        : base("Application Commands", remoteDefinition, logger)
    {
        _applicationLifetime = applicationLifetime;
    }

    protected override Command.ExecuteDelegate CreateHandler(LifecycleCommand command)
        => command.Name switch
        {
            "Exit" => delegate (CancellationToken _)
            {
                _applicationLifetime.StopApplication();
                return Task.CompletedTask;
            }
            ,
            _ => throw new Exception($"Unknown {command}")
        };
}
