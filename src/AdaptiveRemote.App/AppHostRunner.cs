using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

            // Allow tests to inject services before the host is built
            await acceleratedServices.TestEndpoint.InjectHostServiceAsync(hostBuilder, cancellationToken);

            IHost host = BuildHost(hostBuilder);

            await acceleratedServices.TestEndpoint.ProvideServicesToTestAsync(host.Services, cancellationToken);

            await RunHostAsync(host, cancellationToken);
        }
        catch (Exception configErrors)
        {
            acceleratedServices.LoggerFactory
                .CreateLogger<AppHostRunner>()
                .LogError(configErrors, "Fatal error during host configuration or startup.");
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
