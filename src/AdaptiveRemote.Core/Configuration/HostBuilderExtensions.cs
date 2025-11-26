using AdaptiveRemote.Services;
using AdaptiveRemote.Services.Commands;
using AdaptiveRemote.Services.Lifecycle;
using AdaptiveRemote.Services.ProgrammaticSettings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

public static class HostBuilderExtensions
{
    public static IHostBuilder AddRemoteServices(this IHostBuilder builder)
        => builder.ConfigureServices((context, services) => services.AddRemoteServices());

    public static IServiceCollection AddRemoteServices(this IServiceCollection services)
        => services
            .AddHostedService<ApplicationLifecycle>()
            .AddScopedLifecycleService<LifecycleCommandService>()
            .AddScoped<IRemoteDefinitionService, StaticCommandGroupProvider>()
            .AddSingleton<IPersistSettings, PersistSettings>();

    public static IServiceCollection AddScopedLifecycleService<ServiceType>(this IServiceCollection services)
        where ServiceType : class, IScopedLifecycle
        => services.AddScoped<IScopedLifecycle, ServiceType>();

    public static IHostBuilder AddNullCommandSupport<CommandType>(this IHostBuilder hostBuilder)
        where CommandType : Models.Command
        => hostBuilder.ConfigureServices(services => services.AddNullCommandService<CommandType>());

    public static IServiceCollection AddNullCommandService<CommandType>(this IServiceCollection services)
        where CommandType : Models.Command
        => services.AddScopedLifecycleService<NullCommandService<CommandType>>();
}
