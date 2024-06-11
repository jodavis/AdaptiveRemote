using AdaptiveRemote.Services.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Configuration;

internal static class HostBuilderExtensions
{
    internal static IHostBuilder AddRemoteServices(this IHostBuilder builder)
        => builder.ConfigureServices(services => AddRemoteServices(services));

    internal static IServiceCollection AddRemoteServices(this IServiceCollection services)
        => services
            .AddHostedService<Lifecycle.ApplicationLifecycle>()
            .AddSingleton<Lifecycle.IApplicationScopeFactory, Lifecycle.BlazorWindowScopeFactory>()
            .AddScoped<IRemoteDefinitionService, Impl.StaticCommandGroupProvider>()
            .AddSingleton<ICommandService, Commands.CommandService>()
            .AddSingleton<Commands.IApplicationService, Commands.ApplicationService>()
            .AddTiVoServices()
            .AddBroadlinkServices();

    internal static IServiceCollection AddScopedLifecycleService<ServiceType>(this IServiceCollection services)
        where ServiceType : class, IScopedLifecycle
        => services.AddScoped<IScopedLifecycle, ServiceType>();

    private static IServiceCollection AddTiVoServices(this IServiceCollection services)
        => services
            .AddSingleton<ITiVoService, TiVo.PlaceholderTiVoService>();

    private static IServiceCollection AddBroadlinkServices(this IServiceCollection services)
        => services
            .AddSingleton<IBroadlinkService, Broadlink.PlaceholderBroadlinkService>();
}
