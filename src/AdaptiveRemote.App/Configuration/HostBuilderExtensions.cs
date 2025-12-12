using AdaptiveRemote.Services;
using AdaptiveRemote.Services.Commands;
using AdaptiveRemote.Services.Lifecycle;
using AdaptiveRemote.Services.ProgrammaticSettings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

internal static class HostBuilderExtensions
{
    internal static IHostBuilder AddRemoteServices(this IHostBuilder builder)
        => builder.ConfigureServices((context, services) => services.AddRemoteServices());

    internal static IServiceCollection AddRemoteServices(this IServiceCollection services)
        => services
            .AddApplicationLifecycleServices()
            .AddScopedLifecycleService<LifecycleCommandService>()
            .AddScoped<IRemoteDefinitionService, StaticCommandGroupProvider>()
            .AddSingleton<IPersistSettings, PersistSettings>();

    internal static IServiceCollection AddScopedLifecycleService<ServiceType>(this IServiceCollection services)
        where ServiceType : class, IScopedLifecycle
        => services.AddScoped<IScopedLifecycle, ServiceType>();

    internal static IHostBuilder AddNullCommandSupport<CommandType>(this IHostBuilder hostBuilder)
        where CommandType : Models.Command
        => hostBuilder.ConfigureServices(services => services.AddNullCommandService<CommandType>());

    internal static IServiceCollection AddNullCommandService<CommandType>(this IServiceCollection services)
        where CommandType : Models.Command
        => services.AddScopedLifecycleService<NullCommandService<CommandType>>();

    private static IServiceCollection AddApplicationLifecycleServices(this IServiceCollection services)
        => services
            .AddHostedService<ApplicationLifecycle>()
            .AddScoped<ScopedLifecycleContainer>()
            .AddScoped<Components.BlazorAppScope>()
            .AddSingleton<IApplicationScopeContainer, ApplicationScopeContainer>()
            .AddSingleton(sp => (IApplicationScopeProvider)sp.GetRequiredService<IApplicationScopeContainer>());
}
