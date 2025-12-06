using AdaptiveRemote.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Lifecycle;

public class AcceleratedServices
{
    private readonly string[] _args;

    public LifecycleView ViewModel { get; }
    public ILifecycleViewController Controller { get; }
    internal DiagnosticAdapter DiagnosticAdapter { get; }

    public AcceleratedServices(string[] args)
    {
        ViewModel = new();
        Controller = new LifecycleViewController(ViewModel);
        DiagnosticAdapter = new(Controller);

        Controller.SetPhase(LifecyclePhase.Waiting);

        _args = args;
    }

    public void ConfigureHost(IHostBuilder hostBuilder)
    {
        hostBuilder
            .ConfigureAppSettings(_args)
            .ConfigureApp()
            .ConfigureServices(AddPrecreatedServices);
    }

    private void AddPrecreatedServices(IServiceCollection services)
        => services
            .AddSingleton(Controller)
            .AddSingleton(ViewModel);
}
