using AdaptiveRemote.Services.Commands;
using AdaptiveRemote.Services.Lifecycle;
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
            .AddSingleton<IApplicationScopeFactory, BlazorWindowScopeFactory>()
            .AddHostedService<ApplicationLifecycle>()
            .AddScopedLifecycleService<ApplicationCommandService>()
            .AddScoped<IRemoteDefinitionService, StaticCommandGroupProvider>();

    internal static IServiceCollection AddScopedLifecycleService<ServiceType>(this IServiceCollection services)
        where ServiceType : class, IScopedLifecycle
        => services.AddScoped<IScopedLifecycle, ServiceType>();
}
