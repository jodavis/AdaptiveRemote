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
            .AddSingleton<ICommandExecutionService, Impl.CommandExecutionService>();

    internal static IServiceCollection AddScopedLifecycle<ServiceType>(this IServiceCollection services)
        where ServiceType : class, IScopedLifecycle
        => services.AddScoped<IScopedLifecycle, ServiceType>();
}
