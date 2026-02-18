using AdaptiveRemote.Models;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AdaptiveRemote.Services.Lifecycle;

public class AcceleratedServices
{
    public LifecycleView ViewModel { get; }
    public ILifecycleViewController Controller { get; }
    internal DiagnosticAdapter DiagnosticAdapter { get; }
    internal ITestEndpointHooks TestEndpoint { get; }

    public AcceleratedServices(string[] args)
    {
        ViewModel = new();
        Controller = new LifecycleViewController(ViewModel);
        DiagnosticAdapter = new(Controller);
        // TODO: If there's an endpoint port configured in args, create a real test endpoint that listens on that port instead of the disabled one.
        TestEndpoint = new DisabledTestEndpointHooks();

        Controller.SetPhase(LifecyclePhase.Waiting);
    }

    public virtual void AddPrecreatedServices(IServiceCollection services)
        => services
            .AddSingleton(Controller)
            .AddSingleton(ViewModel);
}
