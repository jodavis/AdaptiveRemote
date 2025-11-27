using AdaptiveRemote.Models;
using AdaptiveRemote.Services;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

/// <summary>
/// Public extension methods for configuring the Electron host
/// </summary>
public static class ElectronHostBuilderExtensions
{
    /// <summary>
    /// Configures the application for Electron hosting with fake speech services
    /// </summary>
    public static IHostBuilder ConfigureElectronApp(this IHostBuilder hostBuilder)
        => hostBuilder
            .ConfigureTelemetry()
            .AddRemoteServices()
            .AddBroadlinkSupport()
            .AddTiVoSupport()
            .AddConversationSystem()
            .AddSystemWrapperServices();

    /// <summary>
    /// Adds the lifecycle view model and controller for Electron hosting
    /// </summary>
    public static IServiceCollection AddElectronLifecycle(this IServiceCollection services)
    {
        var lifecycleView = new LifecycleView();
        var lifecycleController = new LifecycleViewController(lifecycleView);
        services.AddSingleton(lifecycleView);
        services.AddSingleton<ILifecycleViewController>(lifecycleController);
        return services;
    }

    /// <summary>
    /// Adds the scope factory for Electron hosting
    /// </summary>
    public static IServiceCollection AddElectronScopeFactory(this IServiceCollection services)
        => services.AddSingleton<IApplicationScopeFactory, ElectronScopeFactory>();
}
