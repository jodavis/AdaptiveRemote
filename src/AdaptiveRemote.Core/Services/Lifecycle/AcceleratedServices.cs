using AdaptiveRemote.Configuration;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Lifecycle;

/// <summary>
/// Manages the accelerated services that are created before the host starts
/// to provide immediate feedback to the user during startup.
/// This class should be used by both Windows and Electron hosts.
/// </summary>
public class AcceleratedServices
{
    private IHostBuilder _hostBuilder;

    /// <summary>
    /// The lifecycle view model used to display startup status
    /// </summary>
    public LifecycleView ViewModel { get; }

    /// <summary>
    /// The lifecycle view controller used to update startup status
    /// </summary>
    public ILifecycleViewController Controller { get; }

    /// <summary>
    /// The diagnostic adapter for telemetry integration
    /// </summary>
    internal DiagnosticAdapter DiagnosticAdapter { get; }

    /// <summary>
    /// Creates a new instance of AcceleratedServices with the given command line arguments
    /// </summary>
    public AcceleratedServices(string[] args)
    {
        ViewModel = new();
        Controller = new LifecycleViewController(ViewModel);
        DiagnosticAdapter = new(Controller);

        Controller.SetPhase(LifecyclePhase.Waiting);

        _hostBuilder = Host.CreateDefaultBuilder(args)
            .ConfigureCoreApp()
            .ConfigureServices(services => services.AddLifecycleServices(ViewModel, Controller));
    }

    /// <summary>
    /// Configure app settings for the host. Call this to set up configuration sources
    /// specific to the host (e.g., user secrets, command line).
    /// </summary>
    public AcceleratedServices ConfigureAppSettings(Action<IConfigurationBuilder> configure)
    {
        _hostBuilder = _hostBuilder.ConfigureAppConfiguration(configure);
        return this;
    }

    /// <summary>
    /// Add host-specific services. Windows host uses this to add MainWindow, BlazorWindowScopeFactory, 
    /// and Windows speech services. Electron host uses this to add ElectronScopeFactory and fake speech services.
    /// </summary>
    public AcceleratedServices ConfigureHostServices(Action<IServiceCollection> configure)
    {
        _hostBuilder = _hostBuilder.ConfigureServices(configure);
        return this;
    }

    /// <summary>
    /// Add host-specific services that require access to configuration.
    /// </summary>
    public AcceleratedServices ConfigureHostServices(Action<HostBuilderContext, IServiceCollection> configure)
    {
        _hostBuilder = _hostBuilder.ConfigureServices(configure);
        return this;
    }

    /// <summary>
    /// Builds and runs the application host
    /// </summary>
    public async Task RunApplicationLoopAsync()
    {
        await Task.Run(async () =>
        {
            try
            {
                IHost host = _hostBuilder.Build();
                await host.RunAsync();
            }
            catch (Exception configErrors)
            {
                Controller.SetFatalError(configErrors);
                throw;
            }
            finally
            {
                Controller.SetPhase(LifecyclePhase.CleaningUp);
            }
        });
    }
}
