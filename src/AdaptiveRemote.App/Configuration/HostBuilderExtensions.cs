using AdaptiveRemote.Contracts;
using AdaptiveRemote.Services;
using AdaptiveRemote.Services.Commands;
using AdaptiveRemote.Services.Layout;
using AdaptiveRemote.Services.Lifecycle;
using AdaptiveRemote.Services.ProgrammaticSettings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Configuration;

internal static class HostBuilderExtensions
{
    internal static IHostBuilder AddRemoteServices(this IHostBuilder builder)
        => builder
            .ConfigureAppConfiguration((ctx, config) =>
            {
                ProgrammaticSettings settings = ctx.Configuration
                    .GetSection(SettingsKeys.ProgrammaticSettings)
                    .Get<ProgrammaticSettings>() ?? new ProgrammaticSettings();
                string path = Environment.ExpandEnvironmentVariables(settings.ProgrammaticSettingsPath);
                config.AddIniFile(path, optional: true, reloadOnChange: false);
            })
            .ConfigureServices((context, services) => services.AddRemoteServices(context.Configuration));

    internal static IServiceCollection AddRemoteServices(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddApplicationLifecycleServices()
            .AddCloudAssetServices(configuration.GetSection(SettingsKeys.CloudSettings))
            .AddScopedCloudAsset(new JsonCloudAsset<CompiledLayout>(
                name: "layout",
                streamUrl: "/notifications/layouts/stream",
                eventName: "layout-ready",
                resourcePath: "/layouts/compiled",
                jsonContext: LayoutContractsJsonContext.Default))
            .AddScopedLifecycleService<LifecycleCommandService>()
            .AddScoped<IRemoteDefinitionService, RemoteLayoutDefinitionService>()
            .AddScoped<IDynamicStylesheetProvider, LayoutStylesheetProvider>()
            .AddSingleton<IPersistSettings, PersistSettings>()
            .Configure<ProgrammaticSettings>(configuration.GetSection(SettingsKeys.ProgrammaticSettings));

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
            .AddSingleton(sp => (IApplicationScopeProvider)sp.GetRequiredService<IApplicationScopeContainer>())
            .AddSingleton<IApplicationRecycleSignal, ApplicationRecycleSignal>();
}
