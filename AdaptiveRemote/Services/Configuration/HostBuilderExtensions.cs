using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Configuration;

internal static class HostBuilderExtensions
{
    internal static IHostBuilder AddRemoteServices(this IHostBuilder builder)
        => builder.ConfigureServices((context, services) => AddRemoteServices(services, context.Configuration));

    internal static IServiceCollection AddRemoteServices(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddHostedService<Lifecycle.ApplicationLifecycle>()
            .AddSingleton<Lifecycle.IApplicationScopeFactory, Lifecycle.BlazorWindowScopeFactory>()
            .AddScoped<IRemoteDefinitionService, Impl.StaticCommandGroupProvider>()
            .AddSingleton<ICommandService, Commands.CommandService>()
            .AddSingleton<Commands.IApplicationService, Commands.ApplicationService>()
            .AddTiVoServices(configuration.GetSection("tivo"))
            .AddBroadlinkServices();

    internal static IServiceCollection AddScopedLifecycleService<ServiceType>(this IServiceCollection services)
        where ServiceType : class, IScopedLifecycle
        => services.AddScoped<IScopedLifecycle, ServiceType>();

    private static IServiceCollection AddTiVoServices(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddSingleton<TiVo.TiVoService>() // TODO: This should be scoped, but it's not created in the right scope
            .AddScoped(services => (ITiVoService)services.GetRequiredService<TiVo.TiVoService>())
            .AddScoped(services => (IScopedLifecycle)services.GetRequiredService<TiVo.TiVoService>())
            .AddSingleton<TiVo.ITiVoConnectionFactory, TiVo.LibraryTiVoConnectionFactory>()
            .AddScoped<TiVo.ITiVoLocator, TiVo.StaticTiVoLocator>()
            .Configure<TiVo.TiVoSettings>(configuration);

    private static IServiceCollection AddBroadlinkServices(this IServiceCollection services)
        => services
            .AddSingleton<IBroadlinkService, Broadlink.PlaceholderBroadlinkService>();
}
