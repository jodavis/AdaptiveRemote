using AdaptiveRemote.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Lifecycle;

internal class LifecycleCommandService : CommandServiceBase<LifecycleCommand>
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILifecycleViewController _viewController;
    private readonly LifecycleView _lifecycleView;
    private CancellationTokenSource? _programmingCts;

    public LifecycleCommandService(IHostApplicationLifetime applicationLifetime, ILifecycleViewController viewController, LifecycleView lifecycleView, IRemoteDefinitionService remoteDefinition, ILogger<LifecycleCommandService> logger)
        : base("Application Commands", remoteDefinition, logger)
    {
        _applicationLifetime = applicationLifetime;
        _viewController = viewController;
        _lifecycleView = lifecycleView;
    }

    protected override Command.ExecuteDelegate CreateHandler(LifecycleCommand command)
        => command.Name switch
        {
            "Exit" => delegate (CancellationToken _)
            {
                _viewController.SetPhase(LifecyclePhase.Stopping);
                _applicationLifetime.StopApplication();
                return Task.CompletedTask;
            }
            ,
            "Learn" => delegate (CancellationToken _)
            {
                _programmingCts = _lifecycleView.EnterProgrammingMode();
                return Task.CompletedTask;
            }
            ,
            _ => throw new Exception($"Unknown {command}")
        };

    protected override Command.ExecuteDelegate? CreateProgramHandler(LifecycleCommand command)
        => command.Name switch
        {
            "Learn" => async delegate (CancellationToken _)
            {
                _lifecycleView.ExitProgrammingMode();
                CancellationTokenSource? cts = Interlocked.Exchange(ref _programmingCts, null);
                if (cts is not null)
                {
                    await cts.CancelAsync();
                    cts.Dispose();
                }
            }
            ,
            _ => null
        };
}
