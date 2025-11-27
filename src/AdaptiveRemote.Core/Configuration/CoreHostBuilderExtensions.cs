using AdaptiveRemote.Models;
using AdaptiveRemote.Services;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

/// <summary>
/// Public extension methods for configuring the cross-platform app
/// </summary>
public static class CoreHostBuilderExtensions
{
    /// <summary>
    /// Configures the core application services shared by all hosts
    /// </summary>
    public static IHostBuilder ConfigureCoreApp(this IHostBuilder hostBuilder)
        => hostBuilder
            .ConfigureTelemetry()
            .AddRemoteServices()
            .AddBroadlinkSupport()
            .AddTiVoSupport()
            .AddConversationSystem()
            .AddSystemWrapperServices();

    /// <summary>
    /// Adds the lifecycle view model and controller for hosting
    /// </summary>
    public static IServiceCollection AddLifecycleServices(this IServiceCollection services, LifecycleView lifecycleView, ILifecycleViewController lifecycleController)
    {
        services.AddSingleton(lifecycleView);
        services.AddSingleton(lifecycleController);
        return services;
    }
}
