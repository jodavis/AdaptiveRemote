using AdaptiveRemote.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

internal class ApplicationCommandService : CommandServiceBase<ApplicationCommand>
{
    private readonly IHostApplicationLifetime _applicationLifetime;

    public ApplicationCommandService(IHostApplicationLifetime applicationLifetime, IRemoteDefinitionService remoteDefinition, ILogger<ApplicationCommandService> logger)
        : base("Application Commands", remoteDefinition)
    {
        _applicationLifetime = applicationLifetime;
        Logger = logger;
    }

    protected override ILogger Logger { get; }

    protected override Command.ExecuteDelegate CreateHandler(ApplicationCommand command)
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
