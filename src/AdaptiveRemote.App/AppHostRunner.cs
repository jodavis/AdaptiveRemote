using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote;

public abstract class AppHostRunner
{
    public AppHostRunner(string[] commandLineArgs)
    {
        CommandLineArguments = commandLineArgs;
    }

    protected string[] CommandLineArguments { get; }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        AcceleratedServices acceleratedServices = CreateAcceleratedServices();

        try
        {
            IHostBuilder hostBuilder = ConfigureHostBuilder()
                .ConfigureServices(acceleratedServices.AddPrecreatedServices)
                .ConfigureAppSettings(CommandLineArguments)
                .ConfigureApp();

            // Capture the service collection so we can pass it to test hooks
            IServiceCollection? serviceCollection = null;
            hostBuilder.ConfigureServices(services => serviceCollection = services);

            // Allow tests to inject services before the host is built
            if (serviceCollection is not null)
            {
                await acceleratedServices.TestEndpoint.InjectHostServiceAsync(hostBuilder, serviceCollection, cancellationToken);
            }

            IHost host = BuildHost(hostBuilder);

            await acceleratedServices.TestEndpoint.ProvideServicesToTestAsync(host.Services, cancellationToken);

            await RunHostAsync(host, cancellationToken);
        }
        catch (Exception configErrors)
        {
            acceleratedServices.Controller.SetFatalError(configErrors);
            throw;
        }
        finally
        {
            acceleratedServices.Controller.SetPhase(LifecyclePhase.CleaningUp);
        }
    }

    protected virtual AcceleratedServices CreateAcceleratedServices() => new(CommandLineArguments);
    protected virtual IHostBuilder ConfigureHostBuilder() => Host.CreateDefaultBuilder(CommandLineArguments);
    protected virtual IHost BuildHost(IHostBuilder hostBuilder) => hostBuilder.Build();
    protected virtual Task RunHostAsync(IHost host, CancellationToken cancellationToken) => host.RunAsync(cancellationToken);
}
