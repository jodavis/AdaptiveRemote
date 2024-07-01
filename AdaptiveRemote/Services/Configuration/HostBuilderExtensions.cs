using AdaptiveRemote.Services.Commands;
using AdaptiveRemote.Services.Lifecycle;
using AdaptiveRemote.Services.TiVo;
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
            .AddHostedService<ApplicationLifecycle>()
            .AddSingleton<IApplicationScopeFactory, BlazorWindowScopeFactory>()
            .AddScoped<IRemoteDefinitionService, StaticCommandGroupProvider>()
            .AddScoped<ICommandService, CommandService>()
            .AddSingleton<IApplicationService, ApplicationService>()
            .AddTiVoServices(configuration.GetSection(SettingsKeys.TiVo))
            .AddBroadlinkServices();

    internal static IServiceCollection AddScopedLifecycleService<ServiceType>(this IServiceCollection services)
        where ServiceType : class, IScopedLifecycle
        => services.AddScoped<IScopedLifecycle, ServiceType>();

    private static IServiceCollection AddTiVoServices(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddScoped<TiVoService>()
            .AddScoped(services => (ITiVoService)services.GetRequiredService<TiVoService>())
            .AddScoped(services => (IScopedLifecycle)services.GetRequiredService<TiVoService>())
            .AddSingleton<ITiVoConnectionFactory, LibraryTiVoConnectionFactory>()
            .AddScoped<ITiVoLocator, StaticTiVoLocator>()
            .Configure<TiVoSettings>(configuration);

    private static IServiceCollection AddBroadlinkServices(this IServiceCollection services)
        => services
            .AddSingleton<IBroadlinkService, Broadlink.PlaceholderBroadlinkService>();
}
