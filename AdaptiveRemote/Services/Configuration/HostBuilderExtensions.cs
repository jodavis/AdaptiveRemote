using AdaptiveRemote.Services.Commands;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Configuration;

internal static class HostBuilderExtensions
{
    internal static IHostBuilder AddRemoteServices(this IHostBuilder builder)
        => builder.ConfigureServices((context, services) => AddRemoteServices(services));

    internal static IServiceCollection AddRemoteServices(this IServiceCollection services)
        => services
            .AddSingleton<IApplicationScopeFactory, BlazorWindowScopeFactory>()
            .AddHostedService<ApplicationLifecycle>()
            .AddScopedLifecycleService<ApplicationCommandService>()
            .AddScoped<IRemoteDefinitionService, StaticCommandGroupProvider>()
            .AddSingleton<IPersistSettings, PersistSettings>();

    internal static IServiceCollection AddScopedLifecycleService<ServiceType>(this IServiceCollection services)
        where ServiceType : class, IScopedLifecycle
        => services.AddScoped<IScopedLifecycle, ServiceType>();
}
