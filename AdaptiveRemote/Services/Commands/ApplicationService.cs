using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Commands;

internal class ApplicationService : IApplicationService
{
    private readonly IHostApplicationLifetime _applicationLifetime;

    public ApplicationService(IHostApplicationLifetime applicationLifetime)
    {
        _applicationLifetime = applicationLifetime;
    }

    void IApplicationService.Exit() => _applicationLifetime.StopApplication();
}
